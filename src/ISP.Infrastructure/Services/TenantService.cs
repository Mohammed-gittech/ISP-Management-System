using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Tenants;
using ISP.Application.Helpers;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ISP.Infrastructure
{
    /// <summary>
    /// خدمة إدارة الوكلاء
    /// </summary>
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<TenantService> _logger;

        public TenantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IPasswordHasher passwordHasher,
            ILogger<TenantService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        /// <summary>
        /// إنشاء وكيل جديد + حساب Admin
        /// </summary>
        public async Task<TenantDto> CreateAsync(CreateTenantDto dto)
        {
            // Check for duplicate email
            var existingTenants = await _unitOfWork.Tenants.GetAllAsync(t => t.ContactEmail == dto.ContactEmail);

            if (existingTenants.Any())
            {
                _logger.LogWarning(
                    "Tenant creation failed — duplicate email | Email:{Email}",
                    EmailHelper.Mask(dto.ContactEmail));

                throw new InvalidOperationException("البريد الإلكتروني موجود مسبقاً");
            }

            // Create tenant
            var tenant = _mapper.Map<Tenant>(dto);
            tenant.CreatedAt = DateTime.UtcNow;

            // Free 
            tenant.IsActive = dto.SubscriptionPlan == TenantPlan.Free;

            // MaxSubscribers  
            tenant.MaxSubscribers = dto.SubscriptionPlan switch
            {
                TenantPlan.Free => 50,
                TenantPlan.Basic => 500,
                TenantPlan.Pro => int.MaxValue,
                _ => 50
            };

            await _unitOfWork.Tenants.AddAsync(tenant);

            // Create tenant subscription
            var subscription = new TenantSubscription
            {
                Tenant = tenant,
                Plan = dto.SubscriptionPlan,
                Price = dto.SubscriptionPlan switch
                {
                    TenantPlan.Free => 0,
                    TenantPlan.Basic => 29 * dto.DurationMonths,
                    TenantPlan.Pro => 99 * dto.DurationMonths,
                    _ => 0
                },
                StartDate = DateTime.UtcNow,
                EndDate = dto.SubscriptionPlan == TenantPlan.Free ? DateTime.UtcNow.AddMonths(1) : DateTime.UtcNow.AddMonths(dto.DurationMonths),
                Status = dto.SubscriptionPlan == TenantPlan.Free
                    ? TenantSubscriptionStatus.Active
                    : TenantSubscriptionStatus.Pending,
                PaymentMethod = "Manual"
            };

            await _unitOfWork.TenantSubscriptions.AddAsync(subscription);

            // Create admin user
            var adminUser = new User
            {
                Tenant = tenant,
                Username = dto.AdminUsername,
                Email = dto.AdminEmail,
                PasswordHash = _passwordHasher.HashPassword(dto.AdminPassword),
                Role = UserRole.TenantAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Users.AddAsync(adminUser);
            await _unitOfWork.SaveChangesAsync();

            // Tenant created successfully
            _logger.LogInformation(
                "Tenant created successfully | Tenant:{TenantId} | Name:{Name} | Plan:{Plan} | IsActive:{IsActive}",
                tenant.Id, tenant.Name, dto.SubscriptionPlan, tenant.IsActive);

            return _mapper.Map<TenantDto>(tenant);
        }

        public async Task<TenantDto?> GetByIdAsync(int id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);
            return tenant == null ? null : _mapper.Map<TenantDto>(tenant);
        }

        public async Task<PagedResultDto<TenantDto>> GetAllAsync(int pageNumber = 1, int pageSize = 10)
        {
            var allTenants = await _unitOfWork.Tenants.GetAllAsync();

            var totalCount = allTenants.Count();
            var items = allTenants
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<TenantDto>
            {
                Items = _mapper.Map<List<TenantDto>>(items),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task UpdateAsync(int id, UpdateTenantDto dto)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);

            if (tenant == null)
            {
                _logger.LogWarning(
                    "Tenant update failed — not found | Tenant:{TenantId}",
                    id);

                throw new InvalidOperationException("الوكيل غير موجود");
            }

            // Track changes for logging
            var changes = new List<string>();

            if (!string.IsNullOrEmpty(dto.ContactEmail))
            {
                changes.Add($"Email:{EmailHelper.Mask(tenant.ContactEmail)}→{EmailHelper.Mask(dto.ContactEmail)}");
                tenant.ContactEmail = dto.ContactEmail;
            }

            if (dto.ContactPhone != null)
            {
                changes.Add($"Phone:{PhoneHelper.Mask(tenant.ContactPhone)}→{PhoneHelper.Mask(dto.ContactPhone)}");
                tenant.ContactPhone = dto.ContactPhone;
            }

            if (dto.TelegramBotToken != null)
            {
                // Do not log token value — sensitive data
                changes.Add("TelegramBotToken:updated");
                tenant.TelegramBotToken = dto.TelegramBotToken;
            }

            if (dto.Address != null)
            {
                changes.Add($"Address:updated");
                tenant.Address = dto.Address;
            }

            if (dto.City != null)
            {
                changes.Add($"City:{tenant.City}→{dto.City}");
                tenant.City = dto.City;
            }

            if (dto.Country != null)
            {
                changes.Add($"Country:{tenant.Country}→{dto.Country}");
                tenant.Country = dto.Country;
            }

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            // Tenant updated successfully
            _logger.LogInformation(
                "Tenant updated successfully | Tenant:{TenantId} | Changes:{Changes}",
                id, string.Join(", ", changes));
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);

            if (tenant == null)
            {
                _logger.LogWarning(
                    "Tenant deactivation failed — not found | Tenant:{TenantId}",
                    id);
                return false;
            }

            tenant.IsActive = false;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            // Deactivating a tenant affects all its users — critical event
            _logger.LogWarning(
                "Tenant deactivated | Tenant:{TenantId} | Name:{Name}",
                id, tenant.Name);

            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);

            if (tenant == null)
            {
                _logger.LogWarning(
                    "Tenant activation failed — not found | Tenant:{TenantId}",
                    id);
                return false;
            }

            tenant.IsActive = true;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation(
                "Tenant activated | Tenant:{TenantId} | Name:{Name}",
                id, tenant.Name);

            return true;
        }

        public async Task<int> GetCurrentSubscribersCountAsync(int tenantId)
        {
            return await _unitOfWork.Subscribers.CountAsync(s => s.TenantId == tenantId);
        }

        public async Task<bool> CanAddSubscriberAsync(int tenantId)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);

            if (tenant == null || !tenant.IsActive)
                return false;

            var currentCount = await GetCurrentSubscribersCountAsync(tenantId);

            return currentCount < tenant.MaxSubscribers;
        }

        /// <summary>
        /// طلب تجديد اشتراك الوكيل — TenantAdmin
        /// ينشئ TenantSubscription جديد بـ Status = Pending
        /// </summary>
        public async Task<TenantSubscriptionDto> RenewRequestAsync(int tenantId, RenewTenantSubscriptionDto dto)
        {
            // Check tenant exists
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);

            if (tenant == null)
            {
                _logger.LogWarning(
                    "Renewal request failed — tenant not found | Tenant:{TenantId}",
                    tenantId);

                throw new InvalidOperationException("الوكيل غير موجود");
            }

            // Check for existing pending request
            var existingPending = await _unitOfWork.TenantSubscriptions
                .GetAllAsync(s => s.TenantId == tenantId
                                && s.Status == TenantSubscriptionStatus.Pending);

            if (existingPending.Any())
            {
                _logger.LogWarning(
                    "Renewal request failed — pending request already exists | Tenant:{TenantId}",
                    tenantId);

                throw new InvalidOperationException("يوجد طلب تجديد معلق بالفعل — انتظر تأكيد SuperAdmin");
            }

            // Create new pending subscription
            var subscription = new TenantSubscription
            {
                TenantId = tenantId,
                Plan = dto.Plan,
                Price = dto.Plan switch
                {
                    TenantPlan.Free => 0,
                    TenantPlan.Basic => 29 * dto.DurationMonths,
                    TenantPlan.Pro => 99 * dto.DurationMonths,
                    _ => 0
                },
                StartDate = DateTime.UtcNow,
                EndDate = dto.Plan == TenantPlan.Free
                                    ? DateTime.UtcNow.AddMonths(1)
                                    : DateTime.UtcNow.AddMonths(dto.DurationMonths),
                Status = TenantSubscriptionStatus.Pending,
                PaymentMethod = "Manual"
            };

            await _unitOfWork.TenantSubscriptions.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            // Renewal request created
            _logger.LogInformation(
                "Renewal request created | Tenant:{TenantId} | Name:{Name} | Plan:{Plan} | Price:{Price}",
                tenantId, tenant.Name, dto.Plan, subscription.Price);

            return _mapper.Map<TenantSubscriptionDto>(subscription);
        }

        /// <summary>
        /// تأكيد استلام الدفع — SuperAdmin فقط
        /// يُفعِّل الـ Tenant وينشئ TenantPayment
        /// </summary>
        public async Task ConfirmPaymentAsync(int tenantId, ConfirmTenantPaymentDto dto)
        {
            // Find pending subscription
            var subscription = await _unitOfWork.TenantSubscriptions
                .GetByIdAsync(dto.SubscriptionId);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "Payment confirmation failed — subscription not found | Subscription:{SubscriptionId}",
                    dto.SubscriptionId);

                throw new InvalidOperationException("الاشتراك غير موجود");
            }

            // Must be pending
            if (subscription.Status != TenantSubscriptionStatus.Pending)
            {
                _logger.LogWarning(
                    "Payment confirmation failed — subscription not pending | Subscription:{SubscriptionId} | Status:{Status}",
                    dto.SubscriptionId, subscription.Status);

                throw new InvalidOperationException("هذا الاشتراك ليس في حالة انتظار");
            }

            // Must belong to this tenant
            if (subscription.TenantId != tenantId)
            {
                _logger.LogWarning(
                    "Payment confirmation failed — subscription does not belong to tenant | Subscription:{SubscriptionId} | Tenant:{TenantId}",
                    dto.SubscriptionId, tenantId);

                throw new InvalidOperationException("الاشتراك لا يخص هذا الوكيل");
            }

            // Activate subscription
            subscription.Status = TenantSubscriptionStatus.Active;
            subscription.LastPaymentDate = DateTime.UtcNow;
            subscription.PaymentMethod = dto.PaymentMethod;

            await _unitOfWork.TenantSubscriptions.UpdateAsync(subscription);

            // Create payment record
            var payment = new TenantPayment
            {
                TenantId = tenantId,
                TenantSubscriptionId = subscription.Id,
                Amount = subscription.Price,
                Currency = "USD",
                PaymentMethod = dto.PaymentMethod,
                PaymentGateway = "Manual",
                TransactionId = dto.TransactionId,
                Status = "Completed",
                Notes = dto.Notes,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.TenantPayments.AddAsync(payment);

            // Activate tenant
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null)
                throw new InvalidOperationException("الوكيل غير موجود");

            tenant.IsActive = true;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            // Payment confirmed — critical financial event
            _logger.LogWarning(
                "Payment confirmed | Tenant:{TenantId} | Name:{Name} | Plan:{Plan} | Amount:{Amount} | Method:{Method} | Transaction:{TransactionId}",
                tenantId, tenant.Name, subscription.Plan, subscription.Price,
                dto.PaymentMethod, dto.TransactionId);
        }

        /// <summary>
        /// عرض كل طلبات التجديد المعلقة — SuperAdmin فقط
        /// </summary>
        public async Task<IEnumerable<TenantSubscriptionDto>> GetPendingRenewalsAsync()
        {
            var pending = await _unitOfWork.TenantSubscriptions
                .GetAllAsync(s => s.Status == TenantSubscriptionStatus.Pending);

            return _mapper.Map<IEnumerable<TenantSubscriptionDto>>(pending);
        }
    }

}