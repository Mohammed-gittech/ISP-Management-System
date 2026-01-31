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
            tenant.IsActive = true;

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
                    TenantPlan.Basic => 29,
                    TenantPlan.Pro => 99,
                    _ => 0
                },
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1),
                Status = TenantSubscriptionStatus.Active,
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
    }
}