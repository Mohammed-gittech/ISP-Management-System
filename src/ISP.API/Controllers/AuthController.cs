using ISP.Application.DTOs.Auth;
using ISP.Application.Interfaces;
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

    }
}