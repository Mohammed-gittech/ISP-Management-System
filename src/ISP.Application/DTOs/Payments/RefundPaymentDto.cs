namespace ISP.Application.DTOs.Payments
{
    /// <summary>
    /// DTO لاسترداد الدفعة
    /// </summary>
    public class RefundPaymentDto
    {
        /// <summary>
        /// المبلغ المراد استرداده (اختياري، إذا لم يُحدد = استرداد كامل)
        /// </summary>
        public decimal? Amount { get; set; }

        /// <summary>
        /// سبب الاسترداد
        /// </summary>
        public string? Reason { get; set; }
    }
}