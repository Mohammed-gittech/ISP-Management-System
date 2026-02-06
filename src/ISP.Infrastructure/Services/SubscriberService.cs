using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة المشتركين
    /// ✅ Soft Delete Support
    /// ✅ Manual Cascade Delete لـ Subscriptions
    /// </summary>
    public class SubscriberService : ISubscriberService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ILogger<SubscriberService> _logger;

        public SubscriberService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenant,
            ILogger<SubscriberService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
            _logger = logger;
        }

        // ============================================
        // Create
        // ============================================

        public async Task<SubscriberDto> CreateAsync(CreateSubscriberDto dto)
        {

            // جلب Tenant الحالي
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentTenant.TenantId);

            if (tenant == null)
            {
                throw new InvalidOperationException("Tenant غير موجود");
            }

            // عدّ المشتركين الحاليين (النشطين فقط)
            var currentSubscribersCount = await _unitOfWork.Subscribers.CountAsync();

            // التحقق من الحد الأقصى
            if (currentSubscribersCount >= tenant.MaxSubscribers)
            {
                throw new InvalidOperationException(
                    $"تم الوصول للحد الأقصى من المشتركين ({tenant.MaxSubscribers}). " +
                    $"يرجى ترقية خطة الاشتراك للإضافة المزيد.");
            }
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

        public async Task<SubscriberDto?> GetByIdAsync(int id)
        {
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(id);
            return subscriber == null ? null : _mapper.Map<SubscriberDto>(subscriber);
        }

        public async Task<PagedResultDto<SubscriberDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10)
        {
            // Repository Filter يطبق Multi-Tenancy + Soft Delete تلقائياً
            var allSubscribers = await _unitOfWork.Subscribers.GetAllAsync();

            // Pagination
            var totalCount = allSubscribers.Count();
            var items = allSubscribers
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

        public async Task<PagedResultDto<SubscriberDto>> SearchAsync(
            string searchTerm,
            int pageNumber = 1,
            int pageSize = 10)
        {
            // البحث في الاسم أو رقم الهاتف
            var filtered = await _unitOfWork.Subscribers.GetAllAsync(s =>
                s.FullName.Contains(searchTerm) || s.PhoneNumber.Contains(searchTerm));

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
        // SOFT DELETE (محدث)
        // ============================================

        /// <summary>
        /// حذف ناعم للمشترك
        /// ✅ يحذف Subscriptions المرتبطة يدوياً (Manual Cascade)
        /// ✅ يحتفظ بالبيانات للاسترجاع
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(id);

            if (subscriber == null)
            {
                throw new InvalidOperationException($"المشترك برقم {id} غير موجود");
            }

            _logger.LogInformation("Soft deleting Subscriber {SubscriberId}", id);

            // ============================================
            // MANUAL CASCADE: حذف Subscriptions المرتبطة
            // ============================================
            var subscriptions = await _unitOfWork.Subscriptions.GetAllAsync(s => s.SubscriberId == id);

            foreach (var subscription in subscriptions)
            {
                await _unitOfWork.Subscriptions.SoftDeleteAsync(subscription);
                _logger.LogInformation("Cascade soft deleted Subscription {SubscriptionId}", subscription.Id);
            }

            // ============================================
            // حذف Subscriber
            // ============================================
            await _unitOfWork.Subscribers.SoftDeleteAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Subscriber {SubscriberId} soft deleted successfully with {Count} subscriptions",
                id, subscriptions.Count());
        }

        // ============================================
        // RESTORE (جديد)
        // ============================================

        /// <summary>
        /// استرجاع مشترك محذوف
        /// ⚠️ لا يسترجع Subscriptions تلقائياً (يجب استرجاعها يدوياً إذا لزم)
        /// </summary>
        public async Task<bool> RestoreAsync(int id)
        {
            _logger.LogInformation("Attempting to restore Subscriber {SubscriberId}", id);

            var restored = await _unitOfWork.Subscribers.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Subscriber {SubscriberId} restored successfully", id);
            }
            else
            {
                _logger.LogWarning("Subscriber {SubscriberId} not found or not deleted", id);
            }

            return restored;
        }

        // ============================================
        // GET DELETED (جديد)
        // ============================================

        /// <summary>
        /// الحصول على المشتركين المحذوفين
        /// </summary>
        public async Task<PagedResultDto<SubscriberDto>> GetDeletedAsync(int pageNumber = 1, int pageSize = 10)
        {
            var deleted = await _unitOfWork.Subscribers.GetDeletedAsync();

            var totalCount = deleted.Count();
            var items = deleted
                .OrderByDescending(s => s.DeletedAt)
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
        // PERMANENT DELETE (جديد - SuperAdmin only)
        // ============================================

        /// <summary>
        /// حذف نهائي للمشترك من Database
        /// ⚠️ SuperAdmin only
        /// ⚠️ لا يمكن التراجع
        /// ✅ يُستخدم بعد انتهاء Retention Period
        /// </summary>
        public async Task<bool> PermanentDeleteAsync(int id)
        {
            _logger.LogWarning("Permanent delete requested for Subscriber {SubscriberId}", id);

            // 1. الحصول على Subscriber (بما فيهم المحذوف)
            var subscriber = await _unitOfWork.Subscribers.GetByIdIncludingDeletedAsync(id);

            if (subscriber == null)
            {
                _logger.LogWarning("Subscriber {SubscriberId} not found for permanent delete", id);
                return false;
            }

            // 2. التحقق من أنه محذوف (Soft Deleted)
            if (!subscriber.IsDeleted)
            {
                throw new InvalidOperationException("لا يمكن الحذف النهائي لمشترك نشط. استخدم Soft Delete أولاً");
            }

            // 3. حذف Subscriptions المرتبطة نهائياً
            var subscriptions = await _unitOfWork.Subscriptions.GetAllIncludingDeletedAsync();
            var subscriberSubs = subscriptions.Where(s => s.SubscriberId == id);

            foreach (var subscription in subscriberSubs)
            {
                await _unitOfWork.Subscriptions.DeleteAsync(subscription);
                _logger.LogInformation("Permanently deleted Subscription {SubscriptionId}", subscription.Id);
            }

            // 4. حذف Subscriber نهائياً
            await _unitOfWork.Subscribers.DeleteAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogWarning("Subscriber {SubscriberId} permanently deleted with {Count} subscriptions",
                id, subscriberSubs.Count());

            return true;
        }

        // ============================================
        // Helper Methods
        // ============================================

        public async Task<bool> PhoneNumberExistsAsync(string phoneNumber, int? excludeId = null)
        {
            var subscribers = await _unitOfWork.Subscribers.GetAllAsync(s => s.PhoneNumber == phoneNumber);

            if (excludeId.HasValue)
                subscribers = subscribers.Where(s => s.Id != excludeId.Value);

            return subscribers.Any();
        }

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