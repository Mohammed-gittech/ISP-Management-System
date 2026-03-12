using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ISP.Application.Helpers
{
    public static class EmailHelper
    {
        public static string Mask(string email)
        {
            // Guard clause — if email is empty return as is
            if (string.IsNullOrWhiteSpace(email))
                return email;

            // Split email into name and domain
            // ahmed@gmail.com → ["ahmed", "gmail.com"]
            var parts = email.Split('@');

            // Guard clause — if invalid email format return as is
            if (parts.Length != 2)
                return email;

            var name = parts[0]; // ahmed
            var domain = parts[1]; // gmail.com

            // Always keep first char + hide rest with *
            // ab@gmail.com    → a*@gmail.com
            // ahmed@gmail.com → a***d@gmail.com
            if (name.Length <= 2)
                return $"{name[0]}*@{domain}";

            // Keep first and last char, hide middle
            return $"{name[0]}***{name[^1]}@{domain}";
        }
    }
}