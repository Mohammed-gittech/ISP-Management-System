using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Invoices;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using ISP.Infrastructure.Services.Pdf;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة الفواتير
    /// </summary>
    public class InvoiceService : IInvoiceService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenant,
            ILogger<InvoiceService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
            _logger = logger;
        }


        // ============================================
        // Invoice Queries
        // ============================================

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(int invoiceId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                return null;

            return await MapInvoiceToDtoAsync(invoice);
        }

        public async Task<InvoiceDto?> GetInvoiceByNumberAsync(string invoiceNumber)
        {
            var invoices = await _unitOfWork.Invoices.GetAllAsync(i => i.InvoiceNumber == invoiceNumber);
            var invoice = invoices.FirstOrDefault();

            if (invoice == null)
                return null;

            return await MapInvoiceToDtoAsync(invoice);
        }

        public async Task<List<InvoiceDto>> GetSubscriberInvoicesAsync(int subscriberId)
        {
            var invoices = await _unitOfWork.Invoices.GetAllAsync(i => i.SubscriberId == subscriberId);

            var dtos = new List<InvoiceDto>();
            foreach (var invoice in invoices.OrderByDescending(i => i.IssuedDate))
            {
                dtos.Add(await MapInvoiceToDtoAsync(invoice));
            }

            return dtos;
        }

        public async Task<PagedResultDto<InvoiceListDto>> GetInvoicesAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? status = null)
        {
            var allInvoices = await _unitOfWork.Invoices.GetAllAsync();

            // Filter by status
            if (!string.IsNullOrEmpty(status))
                allInvoices = allInvoices.Where(i => i.Status == status);

            var totalCount = allInvoices.Count();

            var invoices = allInvoices
                .OrderByDescending(i => i.IssuedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<InvoiceListDto>();
            foreach (var invoice in invoices)
            {
                var subscriber = invoice.SubscriberId.HasValue ? await _unitOfWork.Subscribers.GetByIdAsync(invoice.SubscriberId.Value) : null;
                items.Add(new InvoiceListDto
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    SubscriberName = subscriber?.FullName ?? "Unknown",
                    Total = invoice.Total,
                    Currency = invoice.Currency,
                    Status = invoice.Status,
                    IssuedDate = invoice.IssuedDate,
                    PaidDate = invoice.PaidDate
                });
            }

            return new PagedResultDto<InvoiceListDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // ============================================
        // PDF Generation
        // ============================================

        public async Task<byte[]> GenerateInvoicePdfAsync(int invoiceId)
        {
            var printData = await GetInvoicePrintDataAsync(invoiceId);

            try
            {
                // استخدام طباعة حرارية 80mm فقط
                var generator = new ThermalInvoicePdfGenerator();
                var pdfBytes = generator.GenerateInvoicePdf(printData);

                // Record print
                await RecordPrintAsync(invoiceId);

                _logger.LogInformation("Generated 80mm thermal PDF for invoice {InvoiceId}", invoiceId);

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF for invoice {InvoiceId}", invoiceId);

                // Fallback to placeholder
                var fallbackContent = System.Text.Encoding.UTF8.GetBytes(
                    $"Invoice #{printData.Invoice.InvoiceNumber}\n" +
                    $"Total: {printData.Invoice.Total} {printData.Invoice.Currency}\n" +
                    $"Date: {printData.Invoice.IssuedDate:yyyy-MM-dd}\n\n" +
                    $"Error: {ex.Message}"
                );

                return fallbackContent;
            }
        }

        public async Task<InvoicePrintDto> GetInvoicePrintDataAsync(int invoiceId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"الفاتورة برقم {invoiceId} غير موجودة");

            var invoiceDto = await MapInvoiceToDtoAsync(invoice);

            // Get tenant info
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(_currentTenant.TenantId);

            // Get user who is printing
            var currentUser = _currentTenant.UserId.HasValue ? await _unitOfWork.Users.GetByIdAsync(_currentTenant.UserId.Value) : null;

            // معلومات العميل
            string customerName;
            string? customerPhone = null;
            string? customerAddress = null;

            if (invoice.SubscriberId > 0)
            {
                // مشترك عادي
                var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(invoice.SubscriberId.Value);
                customerName = subscriber?.FullName ?? "Unknown";
                customerPhone = subscriber?.PhoneNumber;
                customerAddress = subscriber?.Address;
            }
            else
            {
                // فاتورة عامة - استخراج معلومات العميل من Notes أو Items
                // نفترض أن الاسم موجود في SubscriberName من DTO
                customerName = invoiceDto.SubscriberName;
                customerPhone = invoiceDto.SubscriberPhone;
                customerAddress = invoiceDto.SubscriberAddress;
            }

            // ============================================
            // بناء عنوان الشركة - NEW ⭐
            // ============================================
            string companyAddress;

            if (!string.IsNullOrEmpty(tenant?.Address))
            {
                // استخدام عنوان الوكيل الفعلي
                var addressParts = new List<string>();

                if (!string.IsNullOrEmpty(tenant.Address))
                    addressParts.Add(tenant.Address);

                if (!string.IsNullOrEmpty(tenant.City))
                    addressParts.Add(tenant.City);

                if (!string.IsNullOrEmpty(tenant.Country))
                    addressParts.Add(tenant.Country);
                else
                    addressParts.Add("Iraq"); // Fallback

                companyAddress = string.Join(", ", addressParts);
            }
            else
            {
                // Fallback للوكلاء بدون عنوان
                companyAddress = "Iraq";
            }

            return new InvoicePrintDto
            {
                CompanyName = tenant?.Name ?? "ISP Company",
                CompanyAddress = companyAddress, // ✅ عنوان ديناميكي
                CompanyPhone = tenant?.ContactPhone,
                CompanyEmail = tenant?.ContactEmail,
                CompanyLogo = null, // TODO: Add logo support
                Invoice = invoiceDto,
                QRCodeData = $"INV:{invoice.InvoiceNumber}|AMT:{invoice.Total}|DATE:{invoice.IssuedDate:yyyyMMdd}",
                BarcodeData = invoice.InvoiceNumber,
                PrintedBy = currentUser?.Username ?? "System",
                PrintedAt = DateTime.UtcNow
            };
        }

        // ============================================
        // Print Tracking
        // ============================================

        public async Task RecordPrintAsync(int invoiceId)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"الفاتورة برقم {invoiceId} غير موجودة");

            invoice.PrintedAt = DateTime.UtcNow;
            invoice.PrintCount++;

            await _unitOfWork.Invoices.UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Invoice {InvoiceNumber} printed (count: {PrintCount})",
                invoice.InvoiceNumber, invoice.PrintCount);
        }

        // ============================================
        // Invoice Management
        // ============================================

        public async Task CancelInvoiceAsync(int invoiceId, string? reason = null)
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"الفاتورة برقم {invoiceId} غير موجودة");

            if (invoice.Status == "Paid")
                throw new InvalidOperationException("لا يمكن إلغاء فاتورة مدفوعة. استخدم الاسترداد بدلاً من ذلك");

            invoice.Status = "Cancelled";
            invoice.Notes = $"{invoice.Notes}\nإلغاء: {reason}";
            invoice.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Invoices.UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Invoice {InvoiceNumber} cancelled. Reason: {Reason}",
                invoice.InvoiceNumber, reason);
        }

        // ============================================
        // Helper Methods
        // ============================================

        private async Task<InvoiceDto> MapInvoiceToDtoAsync(Invoice invoice)
        {
            var subscriber = invoice.SubscriberId.HasValue ? await _unitOfWork.Subscribers.GetByIdAsync(invoice.SubscriberId.Value) : null;

            var dto = _mapper.Map<InvoiceDto>(invoice);
            dto.SubscriberName = subscriber?.FullName ?? "Unknown";
            dto.SubscriberPhone = subscriber?.PhoneNumber ?? "";
            dto.SubscriberAddress = subscriber?.Address;

            // Parse Items from JSON
            if (!string.IsNullOrEmpty(invoice.Items))
            {
                try
                {
                    dto.Items = JsonSerializer.Deserialize<List<InvoiceItemDto>>(invoice.Items)
                        ?? new List<InvoiceItemDto>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing invoice items for invoice {InvoiceId}", invoice.Id);
                    dto.Items = new List<InvoiceItemDto>();
                }
            }

            // Get payment info if exists
            if (invoice.PaymentId.HasValue)
            {
                var payment = await _unitOfWork.Payments.GetByIdAsync(invoice.PaymentId.Value);
                dto.PaymentMethod = payment?.PaymentMethod;
            }

            return dto;
        }
    }
}