using ISP.Domain.Interfaces;
using ISP.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ISP.API.Controllers
{
    /// <summary>
    /// Controller لإدارة Maintenance Operations
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    public class MaintenanceController : ControllerBase
    {
        private readonly RetentionCleanupJob _cleanupJob;
        private readonly IUnitOfWork _unitOfWork;

        public MaintenanceController(RetentionCleanupJob cleanupJob, IUnitOfWork unitOfWork)
        {
            _cleanupJob = cleanupJob;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// تشغيل Retention Cleanup Job يدوياً
        /// ⚠️ SuperAdmin only
        /// ⚠️ يحذف البيانات المحذوفة القديمة نهائياً
        /// </summary>
        [HttpPost("cleanup-old-data")]
        public async Task<IActionResult> CleanupOldData()
        {
            try
            {
                await _cleanupJob.ExecuteAsync();

                return Ok(new
                {
                    success = true,
                    message = "✅ تم تشغيل Cleanup Job بنجاح. راجع Logs للتفاصيل"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ أثناء Cleanup",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// معاينة البيانات التي سيتم حذفها (بدون تنفيذ)
        /// </summary>
        [HttpGet("preview-cleanup")]
        public async Task<IActionResult> PreviewCleanup([FromQuery] int days = 90)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-days);

                var subscribersCount = await _unitOfWork.Subscribers.CountAsync(s => s.IsDeleted && s.DeletedAt < cutoffDate);
                var plansCount = await _unitOfWork.Plans.CountAsync(p => p.IsDeleted && p.DeletedAt < cutoffDate);
                var subscriptionsCount = await _unitOfWork.Subscriptions.CountAsync(s => s.IsDeleted && s.DeletedAt < cutoffDate);
                var usersCount = await _unitOfWork.Users.CountAsync(u => u.IsDeleted && u.DeletedAt < cutoffDate);

                return Ok(new
                {
                    success = true,
                    cutoffDate = cutoffDate.ToString("yyyy-MM-dd"),
                    retentionDays = days,
                    message = "معاينة البيانات التي سيتم حذفها",
                    data = new
                    {
                        subscribers = subscribersCount,
                        plans = plansCount,
                        subscriptions = subscriptionsCount,
                        users = usersCount
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// الحصول على إحصائيات Soft Delete
        /// </summary>
        [HttpGet("soft-delete-stats")]
        public async Task<IActionResult> GetSoftDeleteStats()
        {
            try
            {
                var activeSubscribers = await _unitOfWork.Subscribers.CountAsync();
                var deletedSubscribers = await _unitOfWork.Subscribers.CountAsync(s => s.IsDeleted);

                var activePlans = await _unitOfWork.Plans.CountAsync();
                var deletedPlans = await _unitOfWork.Plans.CountAsync(p => p.IsDeleted);

                var activeSubscriptions = await _unitOfWork.Subscriptions.CountAsync();
                var deletedSubscriptions = await _unitOfWork.Subscriptions.CountAsync(s => s.IsDeleted);

                var activeUsers = await _unitOfWork.Users.CountAsync();
                var deletedUsers = await _unitOfWork.Users.CountAsync(u => u.IsDeleted);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        subscribers = new
                        {
                            active = activeSubscribers,
                            deleted = deletedSubscribers,
                            total = activeSubscribers + deletedSubscribers
                        },
                        plans = new
                        {
                            active = activePlans,
                            deleted = deletedPlans,
                            total = activePlans + deletedPlans
                        },
                        subscriptions = new
                        {
                            active = activeSubscriptions,
                            deleted = deletedSubscriptions,
                            total = activeSubscriptions + deletedSubscriptions
                        },
                        users = new
                        {
                            active = activeUsers,
                            deleted = deletedUsers,
                            total = activeUsers + deletedUsers
                        },

                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "حدث خطأ",
                    error = ex.Message
                });
            }
        }
    }
}