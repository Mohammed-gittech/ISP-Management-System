// ============================================
// ThermalInvoicePdfGenerator.cs - طباعة حرارية 80mm
// ============================================
using ISP.Application.DTOs.Invoices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace ISP.Infrastructure.Services.Pdf
{
    /// <summary>
    /// توليد فواتير حرارية 80mm (الأكثر شيوعاً للوكلاء)
    /// مُحسّن للغة العربية والدول العربية
    /// </summary>
    public class ThermalInvoicePdfGenerator
    {
        // عرض الطابعة الحرارية 80mm = ~302 pixels
        private const float PageWidthInMM = 80;

        public byte[] GenerateInvoicePdf(InvoicePrintDto printData)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // حجم مخصص للطابعة الحرارية 80mm
                    page.Size(new PageSize(PageWidthInMM, 200, Unit.Millimetre));
                    page.Margin(5, Unit.Millimetre);
                    page.PageColor(Colors.White);

                    // خط أكبر قليلاً للوضوح
                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily("Arial") // يدعم العربية جيداً
                        .DirectionFromRightToLeft() // مهم جداً للعربية!
                    );

                    page.Content().Column(column =>
                    {
                        column.Spacing(3);

                        // Header
                        ComposeHeader(column, printData);

                        // Separator
                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        // Invoice Info
                        ComposeInvoiceInfo(column, printData);

                        // Separator
                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        // Customer Info
                        ComposeCustomerInfo(column, printData);

                        // Separator
                        column.Item().PaddingTop(3).Text("العناصر").FontSize(11).Bold().AlignCenter();
                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        // Items
                        ComposeItems(column, printData);

                        // Separator
                        column.Item().LineHorizontal(1).LineColor(Colors.Black);

                        // Totals
                        ComposeTotals(column, printData);

                        // Separator (dashed)
                        column.Item().PaddingVertical(3).LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);

                        // Payment Info
                        ComposePaymentInfo(column, printData);

                        // QR Code
                        ComposeQRCode(column, printData);

                        // Footer
                        ComposeFooter(column, printData);
                    });
                });
            });

            return document.GeneratePdf();
        }

        // ============================================
        // Header - اسم الشركة
        // ============================================
        private void ComposeHeader(ColumnDescriptor column, InvoicePrintDto printData)
        {
            // اسم الشركة (كبير وواضح)
            column.Item().AlignCenter().Text(printData.CompanyName)
                .FontSize(16)
                .Bold()
                .FontColor(Colors.Black);

            // معلومات الشركة
            if (!string.IsNullOrEmpty(printData.CompanyAddress))
                column.Item().AlignCenter().Text(printData.CompanyAddress).FontSize(9);

            if (!string.IsNullOrEmpty(printData.CompanyPhone))
                column.Item().AlignCenter().Text($"{printData.CompanyPhone} : هاتف").FontSize(9);
        }

        // ============================================
        // Invoice Info - رقم الفاتورة والتاريخ
        // ============================================
        private void ComposeInvoiceInfo(ColumnDescriptor column, InvoicePrintDto printData)
        {
            column.Item().PaddingVertical(2).Column(col =>
            {
                col.Item().AlignCenter().Text("فاتورة").FontSize(14).Bold();

                col.Item().AlignCenter().Text($"{printData.Invoice.InvoiceNumber} : رقم").FontSize(10);

                col.Item().AlignCenter().Text($"{printData.Invoice.IssuedDate:yyyy/MM/dd} : التاريخ ")
                    .FontSize(9);

                col.Item().AlignCenter().Text($"{printData.Invoice.IssuedDate:HH:mm} : الوقت")
                    .FontSize(9);
            });
        }

        // ============================================
        // Customer Info - معلومات المشترك
        // ============================================
        private void ComposeCustomerInfo(ColumnDescriptor column, InvoicePrintDto printData)
        {
            column.Item().PaddingVertical(2).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text($"العميل: {printData.Invoice.SubscriberName}").FontSize(10).Bold();

                if (!string.IsNullOrEmpty(printData.Invoice.SubscriberPhone))
                    col.Item().Text($"هاتف: {printData.Invoice.SubscriberPhone}").FontSize(9);

                if (!string.IsNullOrEmpty(printData.Invoice.SubscriberAddress))
                    col.Item().Text($"العنوان: {printData.Invoice.SubscriberAddress}").FontSize(8);
            });
        }

        // ============================================
        // Items - العناصر (مبسط للطباعة الحرارية)
        // ============================================
        private void ComposeItems(ColumnDescriptor column, InvoicePrintDto printData)
        {
            foreach (var item in printData.Invoice.Items)
            {
                column.Item().PaddingVertical(2).AlignRight().Column(itemCol =>
                {
                    // اسم العنصر
                    itemCol.Item().AlignRight().Text(item.Name).FontSize(10).Bold();

                    itemCol.Item().AlignRight().Text($"{item.Quantity} : الكمية").FontSize(9);
                    itemCol.Item().AlignRight().Text($"{item.UnitPrice:N0} : السعر").FontSize(9);

                    // المجموع
                    itemCol.Item().AlignRight().Text($"{item.Total:N0} : المجموع").FontSize(9).Bold()
                        .FontSize(10).Bold();
                });
            }
        }

        // ============================================
        // Totals - الإجمالي
        // ============================================
        private void ComposeTotals(ColumnDescriptor column, InvoicePrintDto printData)
        {
            column.Item().PaddingVertical(3).Column(col =>
            {
                // المجموع الفرعي
                col.Item().Row(row =>
                {
                    row.RelativeItem().AlignLeft().Text($"{printData.Invoice.Subtotal:N0}")
                        .FontSize(10);
                    row.RelativeItem().AlignRight().Text(" :المجموع الفرعي").FontSize(10);
                });

                // الضريبة (إذا وُجدت)
                if (printData.Invoice.Tax > 0)
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignLeft().Text($"{printData.Invoice.Tax:N0}")
                            .FontSize(9);
                        row.RelativeItem().AlignRight().Text(" :الضريبة").FontSize(9);
                    });
                }

                // الخصم (إذا وُجد)
                if (printData.Invoice.Discount > 0)
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignLeft().Text($"-{printData.Invoice.Discount:N0}")
                            .FontSize(9);
                        row.RelativeItem().AlignRight().Text(" :الخصم").FontSize(9);
                    });
                }

                // خط سميك قبل الإجمالي
                col.Item().PaddingVertical(2).LineHorizontal(1.5f).LineColor(Colors.Black);

                // الإجمالي النهائي (كبير وواضح)
                col.Item().Row(row =>
                {
                    row.RelativeItem().AlignLeft()
                        .Text($" {printData.Invoice.Total:N0} {TranslateCurrency(printData.Invoice.Currency)}")
                        .FontSize(12).Bold();
                    row.RelativeItem().AlignRight().Text(" :الإجمالي").FontSize(12).Bold();
                });
            });
        }

        // ============================================
        // Payment Info - طريقة الدفع
        // ============================================
        private void ComposePaymentInfo(ColumnDescriptor column, InvoicePrintDto printData)
        {
            if (!string.IsNullOrEmpty(printData.Invoice.PaymentMethod))
            {
                column.Item().AlignCenter().Text($"طريقةالدفع: {TranslatePaymentMethod(printData.Invoice.PaymentMethod)} ")
                    .FontSize(10);
            }

            if (printData.Invoice.PaidDate.HasValue)
            {
                column.Item().AlignCenter().Text($"الحالة: {TranslateStatus(printData.Invoice.Status)} ")
                    .FontSize(9).Bold().FontColor(Colors.Green.Darken2);
            }
        }

        // ============================================
        // QR Code - للتحقق السريع
        // ============================================
        private void ComposeQRCode(ColumnDescriptor column, InvoicePrintDto printData)
        {
            if (!string.IsNullOrEmpty(printData.QRCodeData))
            {
                var qrBytes = GenerateQRCode(printData.QRCodeData);

                column.Item().PaddingVertical(3).AlignCenter().Column(col =>
                {
                    col.Item().Height(60).Width(60).AlignCenter().Image(qrBytes);
                    col.Item().AlignCenter().Text("امسح للتحقق").FontSize(7).Italic();
                });
            }
        }

        // ============================================
        // Footer - الشكر والملاحظات
        // ============================================
        private void ComposeFooter(ColumnDescriptor column, InvoicePrintDto printData)
        {
            // رسالة شكر
            column.Item().PaddingTop(3).AlignCenter()
                .Text("شكراً لثقتكم بنا").FontSize(11).Bold();

            // ملاحظات (إذا وُجدت)
            if (!string.IsNullOrEmpty(printData.Invoice.Notes))
            {
                column.Item().PaddingTop(2).AlignCenter().Column(col =>
                {
                    col.Item().Text(" ملاحظات").FontSize(8).Bold();
                    col.Item().Text(printData.Invoice.Notes).FontSize(8);
                });
            }

            // معلومات الطباعة
            column.Item().PaddingTop(3).AlignCenter()
                .Text($"{printData.PrintedAt:yyyy/MM/dd HH:mm} :طُبعت")
                .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);

            column.Item().AlignCenter()
                .Text($"{printData.PrintedBy} :بواسطة")
                .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
        }

        // ============================================
        // Helper Methods - الترجمة
        // ============================================

        private string TranslateStatus(string status)
        {
            return status switch
            {
                "Paid" => "مدفوعة ✓",
                "Unpaid" => "غير مدفوعة",
                "Cancelled" => "ملغاة",
                "Refunded" => "مستردة",
                "Draft" => "مسودة",
                "Overdue" => "متأخرة",
                _ => status
            };
        }

        private string TranslatePaymentMethod(string method)
        {
            return method switch
            {
                "Cash" => "نقداً",
                "CreditCard" => "بطاقة ائتمان",
                "BankTransfer" => "تحويل بنكي",
                "Wallet" => "محفظة إلكترونية",
                "ZainCash" => "زين كاش",
                "QiCard" => "كي كارد",
                _ => method
            };
        }

        private string TranslateCurrency(string currency)
        {
            return currency switch
            {
                "IQD" => "دينار",
                "USD" => "دولار",
                "EUR" => "يورو",
                "SAR" => "ريال",
                "AED" => "درهم",
                "KWD" => "دينار كويتي",
                _ => currency
            };
        }

        private byte[] GenerateQRCode(string data)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(10); // حجم أصغر للطباعة الحرارية
        }
    }
}