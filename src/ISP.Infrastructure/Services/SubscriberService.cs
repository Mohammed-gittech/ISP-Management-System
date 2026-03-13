using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Subscribers;
using ISP.Application.Helpers;
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

            // Check tenant exists
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentTenant.TenantId);

            if (tenant == null)
            {
                _logger.LogWarning(
                    "Subscriber creation failed — tenant not found | Tenant:{TenantId}",
                    _currentTenant.TenantId);

                throw new InvalidOperationException("Tenant غير موجود");
            }

            // Check subscriber limit
            var currentSubscribersCount = await _unitOfWork.Subscribers.CountAsync();

            if (currentSubscribersCount >= tenant.MaxSubscribers)
            {
                _logger.LogWarning(
                    "Subscriber creation failed — limit reached | Tenant:{TenantId} | Count:{Count} | Max:{Max}",
                    _currentTenant.TenantId, currentSubscribersCount, tenant.MaxSubscribers);

                throw new InvalidOperationException(
                    $"تم الوصول للحد الأقصى من المشتركين ({tenant.MaxSubscribers}). " +
                    $"يرجى ترقية خطة الاشتراك للإضافة المزيد.");
            }

            // Check duplicate phone
            if (await PhoneNumberExistsAsync(dto.PhoneNumber))
            {
                _logger.LogWarning(
                    "Subscriber creation failed — duplicate phone | Tenant:{TenantId} | Phone:{Phone}",
                    _currentTenant.TenantId, PhoneHelper.Mask(dto.PhoneNumber));

                throw new InvalidOperationException($"رقم الهاتف {dto.PhoneNumber} موجود مسبقاً");
            }

            // Create subscriber
            var subscriber = _mapper.Map<Subscriber>(dto);
            subscriber.TenantId = _currentTenant.TenantId;
            subscriber.RegistrationDate = DateTime.UtcNow;

            await _unitOfWork.Subscribers.AddAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            // Subscriber created successfully
            _logger.LogInformation(
                "Subscriber created successfully | Subscriber:{SubscriberId} | Tenant:{TenantId} | Phone:{Phone}",
                subscriber.Id, subscriber.TenantId, PhoneHelper.Mask(subscriber.PhoneNumber));

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
                _logger.LogWarning(
                    "Subscriber update failed — not found | Subscriber:{SubscriberId}",
                    id);

                throw new InvalidOperationException($"المشترك برقم {id} غير موجود");
            }

            // Track changes for logging
            var changes = new List<string>();

            // Update full name
            if (!string.IsNullOrEmpty(dto.FullName))
            {
                changes.Add("FullName:updated");
                subscriber.FullName = dto.FullName;
            }

            // Update phone
            if (!string.IsNullOrEmpty(dto.PhoneNumber) && dto.PhoneNumber != subscriber.PhoneNumber)
            {
                if (await PhoneNumberExistsAsync(dto.PhoneNumber, id))
                {
                    _logger.LogWarning(
                        "Subscriber update failed — duplicate phone | Subscriber:{SubscriberId} | Phone:{Phone}",
                        id, PhoneHelper.Mask(dto.PhoneNumber));

                    throw new InvalidOperationException($"رقم الهاتف {dto.PhoneNumber} موجود مسبقاً");
                }

                changes.Add($"Phone:{PhoneHelper.Mask(subscriber.PhoneNumber)}→{PhoneHelper.Mask(dto.PhoneNumber)}");
                subscriber.PhoneNumber = dto.PhoneNumber;
            }

            // Update email
            if (dto.Email != null)
            {
                changes.Add($"Email:{EmailHelper.Mask(subscriber.Email)}→{EmailHelper.Mask(dto.Email)}");
                subscriber.Email = dto.Email;
            }

            // Update address
            if (dto.Address != null)
            {
                changes.Add("Address:updated");
                subscriber.Address = dto.Address;
            }

            // Update status
            if (dto.Status.HasValue)
            {
                changes.Add($"Status:{subscriber.Status}→{dto.Status.Value}");
                subscriber.Status = dto.Status.Value;
            }

            // Update notes
            if (dto.Notes != null)
            {
                changes.Add("Notes:updated");
                subscriber.Notes = dto.Notes;
            }

            await _unitOfWork.Subscribers.UpdateAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            // Subscriber updated successfully
            _logger.LogInformation(
                "Subscriber updated successfully | Subscriber:{SubscriberId} | Tenant:{TenantId} | Changes:{Changes}",
                id, subscriber.TenantId, string.Join(", ", changes));
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
                _logger.LogWarning(
                    "Subscriber deletion failed — not found | Subscriber:{SubscriberId}",
                    id);

                throw new InvalidOperationException($"المشترك برقم {id} غير موجود");
            }

            // Cascade soft delete subscriptions
            var subscriptions = await _unitOfWork.Subscriptions.GetAllAsync(s => s.SubscriberId == id);

            foreach (var subscription in subscriptions)
            {
                await _unitOfWork.Subscriptions.SoftDeleteAsync(subscription);
            }

            // Soft delete subscriber
            await _unitOfWork.Subscribers.SoftDeleteAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            // Subscriber soft deleted with cascade
            _logger.LogWarning(
                "Subscriber soft deleted | Subscriber:{SubscriberId} | Tenant:{TenantId} | CascadeDeleted:{Count} subscriptions",
                id, subscriber.TenantId, subscriptions.Count());
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
            var subscriber = await _unitOfWork.Subscribers.GetByIdIncludingDeletedAsync(id);

            if (subscriber == null || !subscriber.IsDeleted)
            {
                _logger.LogWarning(
                    "Subscriber restore failed — not found or not deleted | Subscriber:{SubscriberId}",
                    id);

                return false;
            }

            var restored = await _unitOfWork.Subscribers.RestoreByIdAsync(id);

            if (restored)
            {
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation(
                    "Subscriber restored successfully | Subscriber:{SubscriberId} | Tenant:{TenantId}",
                    id, subscriber.TenantId);
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
            var subscriber = await _unitOfWork.Subscribers.GetByIdIncludingDeletedAsync(id);

            if (subscriber == null)
            {
                _logger.LogWarning(
                    "Subscriber permanent delete failed — not found | Subscriber:{SubscriberId}",
                    id);

                return false;
            }

            if (!subscriber.IsDeleted)
            {
                _logger.LogWarning(
                    "Subscriber permanent delete blocked — not soft deleted | Subscriber:{SubscriberId}",
                    id);

                throw new InvalidOperationException("لا يمكن الحذف النهائي لمشترك نشط. استخدم Soft Delete أولاً");
            }

            // Permanently delete related subscriptions
            var subscriptions = await _unitOfWork.Subscriptions.GetAllIncludingDeletedAsync();
            var subscriberSubs = subscriptions.Where(s => s.SubscriberId == id).ToList();

            foreach (var subscription in subscriberSubs)
                await _unitOfWork.Subscriptions.DeleteAsync(subscription);

            // Permanently delete subscriber
            await _unitOfWork.Subscribers.DeleteAsync(subscriber);
            await _unitOfWork.SaveChangesAsync();

            // Critical — cannot be undone
            _logger.LogCritical(
                "Subscriber PERMANENTLY DELETED | Subscriber:{SubscriberId} | Tenant:{TenantId} | DeletedSubscriptions:{Count}",
                id, subscriber.TenantId, subscriberSubs.Count);

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