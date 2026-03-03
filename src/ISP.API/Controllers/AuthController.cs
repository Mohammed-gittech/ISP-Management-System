using ISP.Application.DTOs.Auth;
using ISP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller للمصادقة (Login)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// تسجيل الدخول
        /// </summary>
        /// <param name="request">Email + Password</param>
        /// <returns>JWT Token + User Info</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);

                if (result == null)
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "البريد الإلكتروني أو كلمة المرور غير صحيحة"
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "تم تسجيل الدخول بنجاح",
                    data = result
                });

            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // ============================================
        // POST api/auth/refresh 
        // ============================================

        /// <summary>
        /// Refresh Token باستخدام Access Token تجديد
        /// </summary>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "مطلوب Refresh Token الـ"
                });
            }

            var result = await _authService.RefreshAccessTokenAsync(request.RefreshToken);

            if (result == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "غير صالح أو منتهي الصلاحية Refresh Token الـ"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم تجديد التوكن بنجاح",
                data = result
            });
        }

        // ============================================
        // POST api/auth/revoke 
        // ============================================

        /// <summary>
        /// Refresh Token تسجيل الخروج — إلغاء 
        /// </summary>
        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "مطلوب Refresh Token الـ"
                });
            }

            var result = await _authService.RevokeRefreshTokenAsync(request.RefreshToken);

            if (!result)
            {
                return NotFound(new
                {
                    success = false,
                    message = "غير موجود أو مُلغى مسبقاً Refresh Token الـ"
                });
            }

            return Ok(new
            {
                success = true,
                message = "تم تسجيل الخروج بنجاح"
            });
        }
    }

}
