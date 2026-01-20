using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;

namespace ISP.Infrastructure.Services
{
    public class SubscriberService : ISubscriberService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;

        public SubscriberService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ICurrentTenantService currentTenant)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
        }

        // ============================================
        // Create
        // ============================================

        /// <summary>
        /// إنشاء مشترك جديد
        /// </summary>
        public async Task<SubscriberDto> CreateAsync(CreateSubscriberDto dto)
        {
            // 1. Validation: التحقق من عدم وجود رقم هاتف مكرر
            if (await PhoneNumberExistsAsync(dto.PhoneNumber))
            {
                throw new InvalidOperationException($"رقم الهاتف {dto.PhoneNumber} موجود مسبقاً");
            }

            // 2. Map DTO → Entity
            var subscriber = _mapper.Map<Subscriber>(dto);

            // 3. تعيين TenantId (Multi-Tenancy)
            subscriber.TenantId = _currentTenant.TenantId;

            // 4. تعيين تاريخ التسجيل
            subscriber.RegistrationDate = DateTime.UtcNow;

            // 5. حفظ في Database
            await _unitOfWork.Subscribers.AddAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<SubscriberDto>(subscriber);
        }

        // ============================================
        // Read
        // ============================================

        /// <summary>
        /// الحصول على مشترك بالـ Id
        /// </summary>
        public async Task<SubscriberDto?> GetByIdAsync(int id)
        {
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(id);

            if (subscriber == null)
                return null;

            return _mapper.Map<SubscriberDto>(subscriber);
        }

        /// <summary>
        /// الحصول على كل المشتركين (مع Pagination)
        /// </summary>
        public async Task<PagedResultDto<SubscriberDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10)
        {
            // Note: Query Filter يطبق تلقائياً (TenantId)
            var allSubscribers = await _unitOfWork.Subscribers.GetAllAsync();

            // Pagination
            var totalCount = allSubscribers.Count();
            var items = allSubscribers
                .Skip((pageNumber - 1) * pageNumber)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<SubscriberDto>
            {
                Items = _mapper.Map<List<SubscriberDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        /// <summary>
        /// البحث عن مشتركين
        /// </summary>
        public async Task<PagedResultDto<SubscriberDto>> SearchAsync(
            string searchTerm,
            int pageNumber = 1,
            int pageSize = 10)
        {
            var allSubscribers = await _unitOfWork.Subscribers.GetAllAsync();

            // البحث في الاسم أو رقم الهاتف
            var filtered = allSubscribers
                .Where(s => s.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.PhoneNumber.Contains(searchTerm))
                .ToList();

            var totalCount = filtered.Count();
            var items = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<SubscriberDto>
            {
                Items = _mapper.Map<List<SubscriberDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // Update
        // ============================================

        /// <summary>
        /// تحديث بيانات مشترك
        /// </summary>
        public async Task UpdateAsync(int id, UpdateSubscriberDto dto)
        {
            // 1. الحصول على المشترك
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(id);

            if (subscriber == null)
            {
                throw new InvalidOperationException($"المشترك برقم {id} غير موجود");
            }

            // 2. التحقق من رقم الهاتف (إذا تم تعديله)
            if (!string.IsNullOrEmpty(dto.PhoneNumber) && dto.PhoneNumber != subscriber.PhoneNumber)
            {
                if (await PhoneNumberExistsAsync(dto.PhoneNumber, id))
                {
                    throw new InvalidOperationException($"رقم الهاتف {dto.PhoneNumber} موجود مسبقاً");
                }
            }

            // 3. تحديث الخصائص (فقط المُرسلة)
            if (!string.IsNullOrEmpty(dto.FullName))
                subscriber.FullName = dto.FullName;

            if (!string.IsNullOrEmpty(dto.PhoneNumber))
                subscriber.PhoneNumber = dto.PhoneNumber;

            if (dto.Email != null)
                subscriber.Email = dto.Email;

            if (dto.Address != null)
                subscriber.Address = dto.Address;

            if (dto.Status.HasValue)
                subscriber.Status = dto.Status.Value;

            if (dto.Notes != null)
                subscriber.Notes = dto.Notes;

            // 4. حفظ التغييرات
            await _unitOfWork.Subscribers.UpdateAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();
        }

        // ============================================
        // Delete
        // ============================================

        /// <summary>
        /// حذف مشترك
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(id);

            if (subscriber == null)
            {
                throw new InvalidOperationException($"المشترك برقم {id} غير موجود");
            }

            // TODO: التحقق من عدم وجود اشتراكات نشطة
            // سنضيفها لاحقاً

            await _unitOfWork.Subscribers.DeleteAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();
        }

        // ============================================
        // Helper Methods
        // ============================================

        /// <summary>
        /// التحقق من وجود رقم هاتف
        /// </summary>
        public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludeId = null)
        {
            var allSubscribers = await _unitOfWork.Subscribers.GetAllAsync();

            return allSubscribers.Any(s =>
                s.PhoneNumber == phoneNumber &&
                s.Id != excludeId
            );
        }

        /// <summary>
        /// ربط مشترك بـ Telegram
        /// </summary>
        public async Task<bool> LinkTelegramAsync(int subscriberId, string chatId)
        {
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(subscriberId);

            if (subscriber == null)
                return false;

            subscriber.TelegramChatId = chatId;

            await _unitOfWork.Subscribers.UpdateAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
    }
}