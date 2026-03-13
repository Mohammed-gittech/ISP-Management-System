namespace ISP.Application.Helpers
{
    public static class PhoneHelper
    {
        public static string Mask(string? phone)
        {
            // Guard clause — if phone is empty return as is
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            // Remove spaces and dashes for consistent processing
            // 079-123-4567 → 07901234567
            var cleaned = phone.Replace("-", "").Replace(" ", "");

            // Too short to mask — return as is
            if (cleaned.Length < 6)
                return phone;

            // Keep first 3 and last 2 chars, mask the middle
            // 07901234567 → 079*****67
            var first = cleaned[..3];
            var last = cleaned[^2..];
            var masked = new string('*', cleaned.Length - 5);

            return $"{first}{masked}{last}";
        }
    }
}