namespace ISP.Domain.Entities
{
    public class RefreshToken
    {
        // Primary Key
        public int Id { get; set; }

        // Token Data 
        public string Token { get; set; } = string.Empty;

        // Ownership — لمن ينتمي هذا التوكن؟
        public int UserId { get; set; }
        public User? User { get; set; } = null!;

        // Lifecycle — دورة حياة التوكن
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }

        // Revocation — الإلغاء
        public bool IsRevoked { get; set; } = false;
        public DateTime? RevokedAt { get; set; }

        // Computed Properties — خصائص محسوبة (لا تُخزن في DB)
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsActive => !IsRevoked && !IsExpired;
    }
}