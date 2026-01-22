using ISP.Application.Interfaces;

namespace ISP.Infrastructure.Identity
{
    /// <summary>
    /// خدمة تشفير كلمات المرور
    /// يستخدم BCrypt
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        /// <summary>
        /// تشفير كلمة المرور
        /// </summary>
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        }

        /// <summary>
        /// التحقق من كلمة المرور
        /// </summary>
        public bool VerifyPassword(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}