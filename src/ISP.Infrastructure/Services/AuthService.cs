using System.Security.Cryptography;
using ISP.Application.DTOs.Auth;
using ISP.Application.Helpers;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
            _configuration = configuration;
            _logger = logger;
        }

        private const int RefreshTokenExpiryDays = 7;
        private int AccessTokenExpiresMinutes =>
            _configuration.GetValue<int>("JWT:AccessTokenExpiresMinutes", 15);

        /// <summary>
        /// تسجيل الدخول
        /// </summary>
        public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request)
        {
            // 1. Find user by email
            var users = await _unitOfWork.Users.GetAllAsync(u => u.Email == request.Email);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                // Unknown email — mask before logging
                _logger.LogWarning(
                    "Login attemp with unknown email | Email:{Email}",
                    EmailHelper.Mask(request.Email));
                return null;
            }

            // 2. Check lockout
            if (user.IsLockedOut)
            {
                _logger.LogWarning(
                    "Login attempt on locked account | User:{UserId} | RemainingMinutes:{Minutes}",
                    user.Id, user.LockoutRemainingMinutes);

                throw new UnauthorizedAccessException(
                    $"الحساب مقفول بسبب محاولات تسجيل دخول متعددة. حاول مجدداً بعد {user.LockoutRemainingMinutes} دقيقة.");
            }

            // 3. Verify password
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning(
                    "Failed login attempt | User:{UserId} | Attempt:{Count}",
                    user.Id, user.FailedLoginAttempts + 1);

                await HandleFailedLoginAsync(user);
                return null;
            }


            // 4. Check active status
            if (!user.IsActive)
            {
                _logger.LogWarning(
                    "Login attempt on inactive account | User:{UserId}",
                    user.Id);

                throw new UnauthorizedAccessException("الحساب معطّل");
            }

            // 5. Check tenant status
            if (user.TenantId.HasValue)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value);
                if (tenant == null || !tenant.IsActive)
                {
                    _logger.LogWarning(
                        "Login attempt on disabled tenant | User:{UserId} | Tenant:{TenantId}",
                        user.Id, user.TenantId);

                    throw new UnauthorizedAccessException("حساب الوكيل معطّل");
                }
            }

            // 6. Reset lockout
            await ResetLockoutAsync(user);

            // 7. Generate tokens
            var accessToken = _jwtTokenService.GenerateToken(user);

            // 8. Refresh Token
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            await _unitOfWork.SaveChangesAsync();

            // Successful login
            _logger.LogInformation(
                "User logged in successfully | User:{UserId} | Tenant:{TenantId} | Role:{Role}",
                user.Id, user.TenantId, user.Role);
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
            // 1. Find token in DB
            var tokens = await _unitOfWork.RefreshTokens
                .GetAllAsync(r => r.Token == refreshToken);

            var existingToken = tokens.FirstOrDefault();

            // 2. Token not found
            if (existingToken == null)
            {
                _logger.LogWarning(
                    "Refresh token not found | Token:{Token}",
                    refreshToken[..10] + "...");

                return null;
            }

            // 3. Token is not active (revoked or expired)
            if (!existingToken.IsActive)
            {
                _logger.LogWarning(
                    "Inactive refresh token used | User:{UserId} | IsRevoked:{IsRevoked} | ExpiresAt:{ExpiresAt}",
                    existingToken.UserId, existingToken.IsRevoked, existingToken.ExpiresAt);

                return null;
            }


            // 4. Find user
            var user = await _unitOfWork.Users.GetByIdAsync(existingToken.UserId);

            if (user == null || !user.IsActive)
            {
                _logger.LogWarning(
                    "Refresh token used for inactive or missing user | User:{UserId}",
                    existingToken.UserId);

                return null;
            }

            // 5. Token Rotation — revoke old token
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;

            await _unitOfWork.RefreshTokens.UpdateAsync(existingToken);

            // 6. Create new refresh token
            var newRefreshToken = await CreateRefreshTokenAsync(user.Id);

            // 7. Save all changes
            await _unitOfWork.SaveChangesAsync();

            // 8. Generate new access token
            var newAccessToken = _jwtTokenService.GenerateToken(user);

            // Token refreshed successfully
            _logger.LogInformation(
                "Access token refreshed successfully | User:{UserId} | Tenant:{TenantId}",
                user.Id, user.TenantId);

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
            // 1. Find token
            var tokens = await _unitOfWork.RefreshTokens
                .GetAllAsync(r => r.Token == refreshToken);

            var existingToken = tokens.FirstOrDefault();

            // 2. Token not found
            if (existingToken == null)
            {
                _logger.LogWarning(
                    "Revoke attempt on non-existent token | Token:{Token}",
                    refreshToken[..10] + "...");

                return false;
            }

            // 3. Already revoked
            if (existingToken.IsRevoked)
            {
                _logger.LogWarning(
                    "Revoke attempt on already revoked token | User:{UserId}",
                    existingToken.UserId);

                return false;
            }

            // 4. Revoke token
            existingToken.IsRevoked = true;
            existingToken.RevokedAt = DateTime.UtcNow;

            await _unitOfWork.RefreshTokens.UpdateAsync(existingToken);
            await _unitOfWork.SaveChangesAsync();

            // Token revoked successfully
            _logger.LogInformation(
                "Refresh token revoked successfully | User:{UserId}",
                existingToken.UserId);

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

                // Account locked — critical security event
                _logger.LogWarning(
                    "Account locked after {Count} failed attempts | User:{UserId} | LockoutEnd:{LockoutEnd}",
                    user.FailedLoginAttempts, user.Id, user.LockoutEnd);
            }
            else
            {
                // Failed attempt — not locked yet
                _logger.LogWarning(
                    "Failed login attempt {Count}/{Max} | User:{UserId}",
                    user.FailedLoginAttempts, maxFailedAttempts, user.Id);
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