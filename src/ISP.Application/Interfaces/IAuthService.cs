
using ISP.Application.DTOs.Auth;

namespace ISP.Application.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto request);
        Task<bool> ValidateTokenAsync(string token);
    }
}