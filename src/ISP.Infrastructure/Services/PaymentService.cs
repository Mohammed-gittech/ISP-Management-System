using AutoMapper;
using ISP.Application.DTOs;
using ISP.Application.DTOs.Payments;
using ISP.Application.Interfaces;
using ISP.Domain.Entities;
using ISP.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ISP.Domain.Enums;
using ISP.Application.DTOs.Invoices;
using Microsoft.EntityFrameworkCore;


namespace ISP.Infrastructure.Services
{
    /// <summary>
    /// خدمة إدارة الدفعات
    /// مع Database-safe sequential numbering
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentTenantService _currentTenant;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentTenantService currentTenant,
            ILogger<PaymentService> logger,
            IInvoiceService invoiceService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentTenant = currentTenant;
            _logger = logger;
        }

        // ============================================
        // Cash Payment Processing
        // ============================================
        public async Task<CreatePaymentResponseDto> ProcessCashPaymentAsync(CreateCashPaymentDto dto)
        {
            _logger.LogInformation("Processing cash payment for Subscriber {SubscriberId}, Amount: {Amount} {Currency}",
                dto.SubscriberId, dto.Amount, dto.Currency);

            // 1. التحقق من وجود المشترك
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(dto.SubscriberId);
            if (subscriber == null)
            {
                throw new InvalidOperationException($"المشترك برقم {dto.SubscriberId} غير موجود");
            }

            // 2. التحقق من الاشتراك (إذا وُجد)
            Subscription? subscription = null;
            if (dto.SubscriptionId.HasValue)
            {
                subscription = await _unitOfWork.Subscriptions.GetByIdAsync(dto.SubscriptionId.Value);
                if (subscription == null)
                {
                    throw new InvalidOperationException($"الاشتراك برقم {dto.SubscriptionId} غير موجود");
                }

                if (subscription.SubscriberId != dto.SubscriberId)
                {
                    throw new InvalidOperationException("الاشتراك لا يخص هذا المشترك");
                }
            }

            // 3. إنشاء Payment
            var payment = new Payment
            {
                TenantId = _currentTenant.TenantId,
                SubscriberId = dto.SubscriberId,
                SubscriptionId = dto.SubscriptionId,
                Amount = dto.Amount,
                Currency = dto.Currency.ToUpper(),
                PaymentMethod = "Cash",
                PaymentGateway = null, // Cash لا يحتاج Gateway
                TransactionId = null,
                Status = "Completed", // Cash دائماً Completed مباشرة
                ReceivedBy = _currentTenant.UserId,
                CashReceiptNumber = dto.CashReceiptNumber,
                Notes = dto.Notes,
                PaidAt = DateTime.UtcNow
            };

            await _unitOfWork.Payments.AddAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Cash payment {PaymentId} created successfully", payment.Id);

            // 4. إنشاء فاتورة (إذا طُلب)
            Invoice? invoice = null;
            if (dto.GenerateInvoice)
            {
                invoice = await CreateInvoiceForPaymentAsync(payment, subscriber, subscription, dto.InvoiceItems);
                payment.InvoiceId = invoice.Id;

                await _unitOfWork.Payments.UpdateAsync(payment);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Invoice {InvoiceNumber} created for payment {PaymentId}",
                    invoice.InvoiceNumber, payment.Id);
            }

            // 5. تحديث الاشتراك (تمديد المدة)
            bool subscriptionRenewed = false;
            DateTime? newEndDate = null;

            if (subscription != null)
            {
                var plan = await _unitOfWork.Plans.GetByIdAsync(subscription.PlanId);
                if (plan != null)
                {
                    // تمديد الاشتراك
                    subscription.EndDate = subscription.EndDate > DateTime.UtcNow
                        ? subscription.EndDate.AddDays(plan.DurationDays)
                        : DateTime.UtcNow.AddDays(plan.DurationDays);

                    subscription.Status = SubscriptionStatus.Active;

                    await _unitOfWork.Subscriptions.UpdateAsync(subscription);
                    await _unitOfWork.SaveChangesAsync();

                    subscriptionRenewed = true;
                    newEndDate = subscription.EndDate;

                    _logger.LogInformation("Subscription {SubscriptionId} renewed until {EndDate}",
                        subscription.Id, newEndDate);
                }
            }

            // 6. إرجاع Response
            return new CreatePaymentResponseDto
            {
                PaymentId = payment.Id,
                InvoiceId = invoice?.Id,
                InvoiceNumber = invoice?.InvoiceNumber,
                Amount = payment.Amount,
                Status = payment.Status,
                PaidAt = payment.PaidAt.Value,
                InvoicePdfUrl = invoice != null ? $"/api/invoices/{invoice.Id}/pdf" : null,
                SubscriptionRenewed = subscriptionRenewed,
                NewSubscriptionEndDate = newEndDate
            };
        }

        // ============================================
        // Helper: Create Invoice
        // ============================================
        private async Task<Invoice> CreateInvoiceForPaymentAsync(
            Payment payment,
            Subscriber subscriber,
            Subscription? subscription,
            List<InvoiceItemDto>? customItems)
        {
            // تحديد عناصر الفاتورة
            List<InvoiceItemDto> items;

            if (customItems != null && customItems.Any())
            {
                // استخدام العناصر المخصصة
                items = customItems;
            }
            else if (subscription != null)
            {
                // استخدام بيانات الاشتراك
                var plan = await _unitOfWork.Plans.GetByIdAsync(subscription.PlanId);
                items = new List<InvoiceItemDto>
                {
                    new InvoiceItemDto
                    {
                        Name = plan?.Name ?? "اشتراك إنترنت",
                        Quantity = 1,
                        UnitPrice = payment.Amount,
                        Description = $"اشتراك {plan?.Speed} ميجا لمدة {plan?.DurationDays} يوم"
                    }
                };
            }
            else
            {
                // دفعة عامة
                items = new List<InvoiceItemDto>
                {
                    new InvoiceItemDto
                    {
                        Name = "دفعة",
                        Quantity = 1,
                        UnitPrice = payment.Amount,
                        Description = "دفعة عامة"
                    }
                };
            }

            // حساب المجاميع
            var subtotal = items.Sum(i => i.Quantity * i.UnitPrice);
            var tax = 0m; // لا ضريبة حالياً
            var discount = 0m; // لا خصم حالياً
            var total = subtotal + tax - discount;

            // إنشاء الفاتورة
            var invoice = new Invoice
            {
                TenantId = _currentTenant.TenantId,
                SubscriberId = subscriber.Id,
                PaymentId = payment.Id,
                InvoiceNumber = await GenerateInvoiceNumberAsync(), // ⭐ Database-safe
                Items = JsonSerializer.Serialize(items),
                Subtotal = subtotal,
                Tax = tax,
                Discount = discount,
                Total = total,
                Currency = payment.Currency,
                Status = "Paid",
                IssuedDate = DateTime.UtcNow,
                PaidDate = DateTime.UtcNow,
                Notes = payment.Notes
            };

            await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            return invoice;
        }

        // ============================================
        // Helper: Generate Invoice Number (DATABASE-SAFE) ⭐⭐⭐
        // ============================================

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var tenantId = _currentTenant.TenantId;

            // ============================================
            // استخدام Database Transaction للأمان الكامل
            // النسخة المبسطة - بدون using var
            // ============================================

            await _unitOfWork.BeginTransactionAsync(); // ⭐ بدون using
            try
            {
                // ⭐ الخطوة 1: جلب/إنشاء العداد
                var counter = await _unitOfWork.InvoiceCounters
                    .GetAllAsync(c => c.TenantId == tenantId && c.Year == year)
                    .ContinueWith(t => t.Result.FirstOrDefault());

                if (counter == null)
                {
                    // إنشاء عداد جديد
                    counter = new InvoiceCounter
                    {
                        TenantId = tenantId,
                        Year = year,
                        LastNumber = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.InvoiceCounters.AddAsync(counter);
                    await _unitOfWork.SaveChangesAsync();
                }

                // ⭐ الخطوة 2: زيادة العداد
                counter.LastNumber++;
                counter.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.InvoiceCounters.UpdateAsync(counter);
                await _unitOfWork.SaveChangesAsync();

                // ⭐ الخطوة 3: Commit Transaction
                await _unitOfWork.CommitTransactionAsync();

                // توليد رقم الفاتورة
                var invoiceNumber = $"INV-{year}-{counter.LastNumber:D5}";

                _logger.LogInformation(
                    "Generated invoice number: {InvoiceNumber} for Tenant {TenantId} (Counter: {LastNumber})",
                    invoiceNumber, tenantId, counter.LastNumber);

                return invoiceNumber;
            }
            catch (Exception ex)
            {
                // Rollback في حالة الخطأ
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Error generating invoice number for Tenant {TenantId}", tenantId);
                throw;
            }
        }


        // ============================================
        // Payment Queries
        // ============================================
        public async Task<PaymentDto?> GetPaymentByIdAsync(int paymentId)
        {
            var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
            if (payment == null)
                return null;

            var dto = _mapper.Map<PaymentDto>(payment);

            // Load related data
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(payment.SubscriberId);
            dto.SubscriberName = subscriber?.FullName ?? "Unknown";

            if (payment.ReceivedBy.HasValue)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(payment.ReceivedBy.Value);
                dto.ReceivedByName = user?.Username ?? "Unknown";
            }

            if (payment.InvoiceId.HasValue)
            {
                var invoice = await _unitOfWork.Invoices.GetByIdAsync(payment.InvoiceId.Value);
                dto.InvoiceNumber = invoice?.InvoiceNumber;
            }

            return dto;
        }

        public async Task<List<PaymentDto>> GetSubscriberPaymentsAsync(int subscriberId)
        {
            var payments = await _unitOfWork.Payments.GetAllAsync(p => p.SubscriberId == subscriberId);
            var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(subscriberId);

            return payments.Select(p =>
            {
                var dto = _mapper.Map<PaymentDto>(p);
                dto.SubscriberName = subscriber?.FullName ?? "Unknown";
                return dto;
            }).OrderByDescending(p => p.CreatedAt).ToList();
        }

        public async Task<PagedResultDto<PaymentListDto>> GetPaymentsAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? status = null,
            string? paymentMethod = null)
        {
            var allPayments = await _unitOfWork.Payments.GetAllAsync();

            // Filters
            if (!string.IsNullOrEmpty(status))
                allPayments = allPayments.Where(p => p.Status == status);

            if (!string.IsNullOrEmpty(paymentMethod))
                allPayments = allPayments.Where(p => p.PaymentMethod == paymentMethod);

            var totalCount = allPayments.Count();

            var payments = allPayments
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = new List<PaymentListDto>();
            foreach (var payment in payments)
            {
                var subscriber = await _unitOfWork.Subscribers.GetByIdAsync(payment.SubscriberId);
                var dto = _mapper.Map<PaymentListDto>(payment);
                dto.SubscriberName = subscriber?.FullName ?? "Unknown";
                items.Add(dto);
            }

            return new PagedResultDto<PaymentListDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PaymentStatsDto> GetPaymentStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.UtcNow.AddMonths(-1);
            toDate ??= DateTime.UtcNow;

            var payments = await _unitOfWork.Payments.GetAllAsync(p =>
                p.Status == "Completed" &&
                p.PaidAt >= fromDate &&
                p.PaidAt <= toDate);

            var cashPayments = payments.Where(p => p.PaymentMethod == "Cash").ToList();
            var onlinePayments = payments.Where(p => p.PaymentMethod != "Cash").ToList();

            return new PaymentStatsDto
            {
                TotalAmount = payments.Sum(p => p.Amount),
                TotalCount = payments.Count(),
                CashAmount = cashPayments.Sum(p => p.Amount),
                CashCount = cashPayments.Count,
                OnlineAmount = onlinePayments.Sum(p => p.Amount),
                OnlineCount = onlinePayments.Count,
                FromDate = fromDate.Value,
                ToDate = toDate.Value
            };
        }

        // ============================================
        // Refunds
        // ============================================

        public async Task<PaymentDto> RefundPaymentAsync(int paymentId, decimal? amount = null, string? reason = null)
        {
            var payment = await _unitOfWork.Payments.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException($"الدفعة برقم {paymentId} غير موجود");

            if (payment.Status == "Refunded")
                throw new InvalidOperationException("الدفعة مستردة مسبقاً");

            var refundAmount = amount ?? payment.Amount;

            if (refundAmount > payment.Amount)
                throw new InvalidOperationException("مبلغ الاسترداد أكبر من مبلغ الدفعة");

            payment.Status = refundAmount == payment.Amount ? "Refunded" : "PartiallyRefunded";
            payment.Notes = $"{payment.Notes}\nاسترداد: {refundAmount} - {reason}";
            payment.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Payments.UpdateAsync(payment);

            // تحديث الفاتورة
            if (payment.InvoiceId.HasValue)
            {
                var invoice = await _unitOfWork.Invoices.GetByIdAsync(payment.InvoiceId.Value);
                if (invoice != null)
                {
                    invoice.Status = "Refunded";
                    invoice.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.Invoices.UpdateAsync(invoice);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Payment {PaymentId} refunded: {Amount}", paymentId, refundAmount);

            return (await GetPaymentByIdAsync(paymentId))!;
        }
    }
}