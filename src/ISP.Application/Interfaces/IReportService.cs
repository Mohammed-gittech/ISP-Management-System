

using ISP.Application.DTOs.Reports;

namespace ISP.Application.Interfaces
{
    /// <summary>
    /// Interface لخدمة التقارير والإحصائيات
    /// يوفر تقارير متنوعة لمساعدة الوكيل في اتخاذ القرارات
    /// </summary>
    public interface IReportService
    {
        // ============================================
        // 1. Revenue Report (تقرير الإيرادات)
        // ============================================

        /// <summary>
        /// الحصول على تقرير الإيرادات لفترة محددة
        /// </summary>
        Task<RevenueReportDto> GetRevenueReportAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// الحصول على تقرير نمو المشتركين
        /// </summary>
        Task<GrowthReportDto> GetGrowthReportAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// الحصول على تقرير شعبية الباقات
        /// </summary>
        Task<PlanPopularityReportDto> GetPlanPopularityReportAsync(DateTime? startDate = null, DateTime? endDate = null, int? top = null);

        /// <summary>
        /// الحصول على تقرير الاشتراكات المنتهية قريباً
        /// </summary>
        Task<ExpiringSoonReportDto> GetExpiringSoonReportAsync(int? days = 7);

        /// <summary>
        /// الحصول على ملخص Dashboard - نظرة شاملة سريعة
        /// </summary>
        Task<DashboardSummaryDto> GetDashboardSummaryAsync();

    }
}