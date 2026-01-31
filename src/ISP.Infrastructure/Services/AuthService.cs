using ISP.Application.DTOs.Auth;
using ISP.Application.Interfaces;
using ISP.Domain.Interfaces;

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

        public AuthService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
        }

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

            // 2. التحقق من كلمة المرور
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                return null;

            // 3. التحقق من أن الحساب نشط
            if (!user.IsActive)
                throw new UnauthorizedAccessException("الحساب معطّل");

            // 4. التحقق من أن Tenant نشط (إذا لم يكن SuperAdmin)
            if (user.TenantId.HasValue)
            {
                var tenant = await _unitOfWork.Tenants.GetByIdAsync(user.TenantId.Value);
                if (tenant == null || !tenant.IsActive)
                    throw new UnauthorizedAccessException("حساب الوكيل معطّل");
            }

            // 5. توليد JWT Token
            var token = _jwtTokenService.GenerateToken(user);

            // 6. إرجاع Response
            return new LoginResponseDto
            {
                Token = token,
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
    }
}