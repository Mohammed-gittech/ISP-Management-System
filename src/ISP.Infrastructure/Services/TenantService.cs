using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Tenants;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Enums;
using ISP.Domain.Interfaces;

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

        public TenantService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// إنشاء وكيل جديد + حساب Admin
        /// </summary>
        public async Task<TenantDto> CreateAsync(CreateTenantDto dto)
        {
            // 1. التحقق من عدم وجود Email مكرر
            var existingTenants = await _unitOfWork.Tenants.GetAllAsync(t => t.ContactEmail == dto.ContactEmail);
            if (existingTenants.Any())
            {
                throw new InvalidOperationException("البريد الإلكتروني موجود مسبقاً");
            }

            // 2. إنشاء Tenant
            var tenant = _mapper.Map<Tenant>(dto);
            tenant.CreatedAt = DateTime.UtcNow;

            // ✅ Free مفعل فوراً — غيره ينتظر الدفع
            tenant.IsActive = dto.SubscriptionPlan == TenantPlan.Free;

            // تحديد MaxSubscribers حسب الباقة
            tenant.MaxSubscribers = dto.SubscriptionPlan switch
            {
                TenantPlan.Free => 50,
                TenantPlan.Basic => 500,
                TenantPlan.Pro => int.MaxValue,
                _ => 50
            };

            await _unitOfWork.Tenants.AddAsync(tenant);

            // 3. إنشاء TenantSubscription
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
                // ✅ Free مفعل — غيره Pending حتى الدفع
                Status = dto.SubscriptionPlan == TenantPlan.Free
                    ? TenantSubscriptionStatus.Active
                    : TenantSubscriptionStatus.Pending,
                PaymentMethod = "Manual"
            };

            await _unitOfWork.TenantSubscriptions.AddAsync(subscription);

            // 4. إنشاء Admin User
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

            // 5. حفظ الكل
            await _unitOfWork.SaveChangesAsync();

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
                throw new InvalidOperationException("الوكيل غير موجود");

            if (!string.IsNullOrEmpty(dto.Name))
                tenant.Name = dto.Name;

            if (!string.IsNullOrEmpty(dto.ContactEmail))
                tenant.ContactEmail = dto.ContactEmail;

            if (dto.ContactPhone != null)
                tenant.ContactPhone = dto.ContactPhone;

            if (dto.TelegramBotToken != null)
                tenant.TelegramBotToken = dto.TelegramBotToken;

            if (dto.Address != null)
                tenant.Address = dto.Address;

            if (dto.City != null)
                tenant.City = dto.City;

            if (dto.Country != null)
                tenant.Country = dto.Country;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);

            if (tenant == null)
                return false;

            tenant.IsActive = false;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(id);

            if (tenant == null)
                return false;

            tenant.IsActive = true;

            await _unitOfWork.Tenants.UpdateAsync(tenant);
            await _unitOfWork.SaveChangesAsync();

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
            // 1. التحقق من وجود الـ Tenant
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null)
                throw new InvalidOperationException("الوكيل غير موجود");

            // 2. التحقق من عدم وجود طلب معلق مسبقاً
            var existingPending = await _unitOfWork.TenantSubscriptions
                .GetAllAsync(s => s.TenantId == tenantId
                                && s.Status == TenantSubscriptionStatus.Pending);

            if (existingPending.Any())
                throw new InvalidOperationException("يوجد طلب تجديد معلق بالفعل — انتظر تأكيد SuperAdmin");

            // 3. إنشاء TenantSubscription جديد بـ Status = Pending
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

            // 4. حفظ
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<TenantSubscriptionDto>(subscription);
        }

        /// <summary>
        /// تأكيد استلام الدفع — SuperAdmin فقط
        /// يُفعِّل الـ Tenant وينشئ TenantPayment
        /// </summary>
        public async Task ConfirmPaymentAsync(int tenantId, ConfirmTenantPaymentDto dto)
        {
            // 1. جلب TenantSubscription المعلق بالـ Id
            var subscription = await _unitOfWork.TenantSubscriptions
                .GetByIdAsync(dto.SubscriptionId);

            if (subscription == null)
                throw new InvalidOperationException("الاشتراك غير موجود");

            // 2. التحقق أنه Pending — لا نؤكد اشتراكاً مفعلاً مسبقاً
            if (subscription.Status != TenantSubscriptionStatus.Pending)
                throw new InvalidOperationException("هذا الاشتراك ليس في حالة انتظار");

            // 3. التحقق أن الاشتراك يخص هذا الـ Tenant
            if (subscription.TenantId != tenantId)
                throw new InvalidOperationException("الاشتراك لا يخص هذا الوكيل");

            // 4. تحديث TenantSubscription
            subscription.Status = TenantSubscriptionStatus.Active;
            subscription.LastPaymentDate = DateTime.UtcNow;
            subscription.PaymentMethod = dto.PaymentMethod;

            await _unitOfWork.TenantSubscriptions.UpdateAsync(subscription);

            // 5. إنشاء TenantPayment — سجل مالي كامل
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

            // 6. تفعيل الـ Tenant
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);
            if (tenant == null)
                throw new InvalidOperationException("الوكيل غير موجود");

            tenant.IsActive = true;
            await _unitOfWork.Tenants.UpdateAsync(tenant);

            // 7. حفظ الكل في SaveChanges واحد
            // لو واحد فشل → كلهم Rollback
            await _unitOfWork.SaveChangesAsync();
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