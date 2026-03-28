namespace ISP.Application.DTOs.Payments
{
    /// <summary>
    /// إحصائيات الدفعات
    /// </summary>
    public class PaymentStatsDto
    {
        public decimal TotalAmount { get; set; }
        public int TotalCount { get; set; }
        public decimal CashAmount { get; set; }
        public int CashCount { get; set; }
        public decimal OnlineAmount { get; set; }
        public int OnlineCount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }
}