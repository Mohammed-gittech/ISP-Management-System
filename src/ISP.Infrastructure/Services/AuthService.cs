using System.Security.Cryptography;
using ISP.Application.DTOs.Auth;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة المصادقة (Authentication)
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
            _configuration = configuration;
        }

        private const int RefreshTokenExpiryDays = 7;
        private int AccessTokenExpiresMinutes =>
            _configuration.GetValue<int>("JWT:AccessTokenExpiresMinutes", 15);

        /// <summary>
        /// تسجيل الدخول
        /// </summary>
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            // 1. البحث عن المستخدم بالـ Email
            var users = await _unitOfWork.Users.GetAllAsync(u => u.Email == request.Email);
            var user = users.FirstOrDefault();

            if (user == null)
                return null;

            // 2. هل الحساب مقفول؟
            if (user.IsLockedOut)
            {
                // throw new UnauthorizedAccessException(
                //     $"  .دقيقة {user.LockoutRemainingMinutes} الحساب مقفول بسبب محاولات تسجيل دخول متعددة. حاول مجدداً بعد" +
                //     $" ");
                throw new UnauthorizedAccessException(
                    $"الحساب مقفول بسبب محاولات تسجيل دخول متعددة. حاول مجدداً بعد {user.LockoutRemainingMinutes} دقيقة.");

            }
            // 3. التحقق من كلمة المرور
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                await HandleFailedLoginAsync(user);
                return null;
            }


            // 4. التحقق من أن الحساب نشط
            if (!user.IsActive)
                throw new UnauthorizedAccessException("الحساب معطّل");

            // 5. التحقق من أن Tenant نشط (إذا لم يكن SuperAdmin)
            if (user.TenantId.HasValue)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value);
                if (tenant == null || !tenant.IsActive)
                    throw new UnauthorizedAccessException("حساب الوكيل معطّل");
            }

            // 6. كلمة المرور صحيحة — صفِّر العداد
            await ResetLockoutAsync(user);

            // 7. توليد Access Token
            var accessToken = _jwtTokenService.GenerateToken(user);

            // 8. Refresh Token
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            await _unitOfWork.SaveChangesAsync();

            return new LoginResponseDto
            {
                Token = accessToken,
                RefreshToken = refreshToken.Token,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiresMinutes),
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString(),
                TenantId = user.TenantId,
                TenantName = user.Tenant?.Name
            };
        }

        /// <summary>
        /// التحقق من صلاحية Token
        /// </summary>
        public async Task<bool> ValidateTokenAsync(string token)
        {
            var userId = _jwtTokenService.ValidateToken(token);

            if (userId == null)
                return false;

            var user = await _unitOfWork.Users.GetByIdAsync(userId.Value);

            return user != null && user.IsActive;
        }

        // ============================================
        // RefreshAccessTokenAsync 
        // ============================================
        public async Task<LoginResponseDto?> RefreshAccessTokenAsync(string refreshToken)
        {
            // 1. DB في Token ابحث عن الـ
            var tokens = await _unitOfWork.RefreshTokens
                .GetAllAsync(r => r.Token == refreshToken);

            var existingToken = tokens.FirstOrDefault();

            // 2. هل وُجد؟
            if (existingToken == null)
                return null;

            // 3. هل لا يزال صالحاً؟
            if (!existingToken.IsActive)
                return null;
            // IsActive = !IsRevoked && !IsExpired

            // 4. جلب المستخدم
            var user = await _unitOfWork.Users.GetByIdAsync(existingToken.UserId);

            if (user == null || !user.IsActive)
                return null;

            // 5. ← Token Rotation: إلغاء القديم
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;

            await _unitOfWork.RefreshTokens.UpdateAsync(existingToken);

            // 6. إنشاء Refresh Token جديد
            var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

            // 7. حفظ كل التغييرات دفعة واحدة
            await _unitOfWork.SaveChangesAsync();

            // 8. توليد Access Token جديد
            var newAccessToken = _jwtTokenService.GenerateToken(user);

            return new LoginResponseDto
            {
                Token = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(AccessTokenExpiresMinutes),
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString(),
                TenantId = user.TenantId,
                TenantName = user.Tenant?.Name
            };

        }

        // ============================================
        // RevokeRefreshTokenAsync 
        // ============================================
        public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
        {
            // 1. ابحث عن التوكن
            var tokens = await _unitOfWork.RefreshTokens
                .GetAllAsync(r => r.Token == refreshToken);

            var existingToken = tokens.FirstOrDefault();

            // 2. لو لم يوجد
            if (existingToken == null)
                return false;

            // 3. لو موجود لكن أُلغي مسبقاً
            if (existingToken.IsRevoked)
                return false;

            // 4. إلغاؤه
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;

            await _unitOfWork.RefreshTokens.UpdateAsync(existingToken);
            await _unitOfWork.SaveChangesAsync();

            // true = تم الإلغاء بنجاح
            return true;
        }

        // ============================================
        // HandleFailedLoginAsync ← Private Helper 
        // ============================================

        private async Task HandleFailedLoginAsync(User user)
        {
            var maxFailedAttempts = _configuration
                .GetValue<int>("AccountLockout:MaxFailedAttempts", 5);

            var lockoutDurationMinutes = _configuration
                .GetValue<int>("AccountLockout:LockoutDurationMinutes", 15);

            user.FailedLoginAttempts++;

            user.LastFailedLoginAt = DateTime.UtcNow;

            if (user.FailedLoginAttempts >= maxFailedAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
            }

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        // ============================================
        // ResetLockoutAsync ← Private Helper 
        // ============================================

        private async Task ResetLockoutAsync(User user)
        {
            if (user.FailedLoginAttempts == 0 && user.LockoutEnd == null)
                return;

            user.FailedLoginAttempts = 0;

            user.LockoutEnd = null;

            user.LastFailedLoginAt = null;

            await _unitOfWork.Users.UpdateAsync(user);
        }

        // ============================================
        // CreateRefreshTokenAsync ← Private Helper
        // ============================================
        private async Task<RefreshToken> CreateRefreshTokenAsync(int userId)
        {
            // 1. توليد النص العشوائي
            var randomBytes = new Byte[64];

            // RandomNumberGenerator = مولّد أرقام عشوائية آمن أمنياً
            using var rng = RandomNumberGenerator.Create();

            rng.GetBytes(randomBytes);

            var tokenString = Convert.ToBase64String(randomBytes);

            // 2. إنشاء الـ Entity
            var refreshToken = new RefreshToken
            {
                Token = tokenString,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
                IsRevoked = false,
            };

            // 3. حفظ في DB
            await _unitOfWork.RefreshTokens.AddAsync(refreshToken);

            return refreshToken;
        }
    }
}