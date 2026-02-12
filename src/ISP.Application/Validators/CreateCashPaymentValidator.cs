// ============================================
// CreateCashPaymentValidator.cs
// ============================================
using FluentValidation;
using ISP.Application.DTOs.Payments;

namespace ISP.Application.Validators
{
    /// <summary>
    /// Validator لإنشاء دفعة كاش
    /// </summary>
    public class CreateCashPaymentValidator : AbstractValidator<CreateCashPaymentDto>
    {
        public CreateCashPaymentValidator()
        {
            // SubscriberId مطلوب
            RuleFor(x => x.SubscriberId)
                .GreaterThan(0)
                .WithMessage("معرف المشترك مطلوب");

            // Amount مطلوب وأكبر من صفر
            RuleFor(x => x.Amount)
                .GreaterThan(0)
                .WithMessage("المبلغ يجب أن يكون أكبر من صفر")
                .LessThanOrEqualTo(10000000)
                .WithMessage("المبلغ كبير جداً");

            // Currency
            RuleFor(x => x.Currency)
                .NotEmpty()
                .WithMessage("العملة مطلوبة")
                .MaximumLength(3)
                .WithMessage("العملة يجب أن تكون 3 أحرف فقط (IQD, USD, EUR)")
                .Must(BeValidCurrency)
                .WithMessage("العملة غير صحيحة. العملات المسموحة: IQD, USD, EUR");

            // CashReceiptNumber (اختياري)
            RuleFor(x => x.CashReceiptNumber)
                .MaximumLength(100)
                .When(x => !string.IsNullOrEmpty(x.CashReceiptNumber))
                .WithMessage("رقم الإيصال طويل جداً");

            // Notes (اختياري)
            RuleFor(x => x.Notes)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrEmpty(x.Notes))
                .WithMessage("الملاحظات طويلة جداً");

            // InvoiceItems validation (إذا وُجدت)
            RuleForEach(x => x.InvoiceItems)
                .ChildRules(item =>
                {
                    item.RuleFor(i => i.Name)
                        .NotEmpty()
                        .WithMessage("اسم العنصر مطلوب")
                        .MaximumLength(200);

                    item.RuleFor(i => i.Quantity)
                        .GreaterThan(0)
                        .WithMessage("الكمية يجب أن تكون أكبر من صفر");

                    item.RuleFor(i => i.UnitPrice)
                        .GreaterThanOrEqualTo(0)
                        .WithMessage("السعر يجب أن يكون صفر أو أكثر");
                })
                .When(x => x.InvoiceItems != null && x.InvoiceItems.Any());
        }

        private bool BeValidCurrency(string currency)
        {
            var validCurrencies = new[] { "IQD", "USD", "EUR", "GBP" };
            return validCurrencies.Contains(currency.ToUpper());
        }
    }
}