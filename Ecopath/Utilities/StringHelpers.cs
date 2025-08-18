using System.Text;
using System.Text.RegularExpressions;

namespace Ecopath.Utilities
{
    public class StringHelpers
    {
        /// <summary>
        /// Normalize a name by changing any punctuation with spaces, and converting
        /// the name to invariant lowercase.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string NormalizeName(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return string.Empty;

            // Normalize to Unicode NFC
            string normalized = str.Normalize(NormalizationForm.FormC);
            // Lowercase invariant
            string invariant = normalized.ToLowerInvariant();
            // Replace non-letter/digit characters with space
            normalized = Regex.Replace(normalized, @"[^\p{L}\p{N}]+", " ");
            // Split
            string[] bits = normalized.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            // Split on space and rejoin
            string joined = string.Join(" ", normalized.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            // Final lowercase (after all processing)
            return joined.ToLowerInvariant();
        }
    }
}
