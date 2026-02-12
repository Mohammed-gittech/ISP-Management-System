// ============================================
// PaymentMethod.cs
// ============================================
namespace ISP.Domain.Enums
{
    /// <summary>
    /// طرق الدفع
    /// </summary>
    public enum PaymentMethod
    {
        /// <summary>
        /// نقداً (Cash)
        /// </summary>
        Cash = 1,

        /// <summary>
        /// بطاقة ائتمان (Online)
        /// </summary>
        CreditCard = 2,

        /// <summary>
        /// تحويل بنكي
        /// </summary>
        BankTransfer = 3,

        /// <summary>
        /// محفظة إلكترونية
        /// </summary>
        Wallet = 4,

        /// <summary>
        /// ZainCash
        /// </summary>
        ZainCash = 5,

        /// <summary>
        /// Qi Card
        /// </summary>
        QiCard = 6,

        /// <summary>
        /// أخرى
        /// </summary>
        Other = 99
    }
}