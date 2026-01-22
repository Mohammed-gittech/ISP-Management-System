using ISP.Domain.Entities;

namespace ISP.Application.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateToken(User user);
        int? ValidateToken(string token);
    }
}