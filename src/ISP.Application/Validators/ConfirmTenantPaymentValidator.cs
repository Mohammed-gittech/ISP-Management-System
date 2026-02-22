// ============================================
// ConfirmTenantPaymentValidator.cs
// التحقق من صحة تأكيد الدفع
// ============================================

using FluentValidation;
using ISP.Application.DTOs.Tenants;

namespace ISP.Application.Validators
{
    public class ConfirmTenantPaymentValidator : AbstractValidator<ConfirmTenantPaymentDto>
    {
        public ConfirmTenantPaymentValidator()
        {
            // SubscriptionId يجب أن يكون رقماً موجباً
            RuleFor(x => x.SubscriptionId)
                .GreaterThan(0)
                .WithMessage("يجب تحديد الاشتراك المراد تأكيده");

            // PaymentMethod مطلوب
            RuleFor(x => x.PaymentMethod)
                .NotEmpty()
                .WithMessage("طريقة الدفع مطلوبة")
                .MaximumLength(50)
                .WithMessage("طريقة الدفع لا يمكن أن تتجاوز 50 حرفاً");

            // TransactionId اختياري لكن له حد أقصى
            RuleFor(x => x.TransactionId)
                .MaximumLength(255)
                .WithMessage("رقم العملية لا يمكن أن يتجاوز 255 حرفاً")
                .When(x => !string.IsNullOrEmpty(x.TransactionId));

            // Notes اختيارية لكن لها حد أقصى
            RuleFor(x => x.Notes)
                .MaximumLength(1000)
                .WithMessage("الملاحظات لا يمكن أن تتجاوز 1000 حرف")
                .When(x => !string.IsNullOrEmpty(x.Notes));
        }
    }
}