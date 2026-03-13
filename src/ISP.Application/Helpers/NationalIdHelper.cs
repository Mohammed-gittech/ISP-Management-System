namespace ISP.Application.Helpers
{
    public static class NationalIdHelper
    {
        public static string Mask(string? nationalId)
        {
            // Guard clause — if nationalId is empty return as is
            if (string.IsNullOrWhiteSpace(nationalId))
                return string.Empty;

            // Remove spaces for consistent processing
            var cleaned = nationalId.Replace(" ", "");

            // Too short to mask — return as is
            if (cleaned.Length < 6)
                return nationalId;

            // Keep first 3 and last 2 chars, mask the middle
            // 12345678901 → 123******01
            var first = cleaned[..3];
            var last = cleaned[^2..];
            var masked = new string('*', cleaned.Length - 5);

            return $"{first}{masked}{last}";
        }
    }
}