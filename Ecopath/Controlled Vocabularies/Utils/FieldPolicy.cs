using ControlledVocabularies.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace ControlledVocabularies.Utils
{
    public static class FieldPolicy
    {
        /// <summary>
        /// Schema identifiers: vocabulary names, column/field names, registry keys.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string ForSchema(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var nfc = s.Normalize(NormalizationForm.FormC).ToLowerInvariant();

            // Preserve dots (namespace separators) and replace other non-alphanumeric with dashes
            nfc = Regex.Replace(nfc, @"[^\p{L}\p{N}\.]+", "-");  // Note: \. added

            return string.Join("-", nfc.Split("-", StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// normalize on demand based on the *type* of value.
        /// Use at comparison time (matchers), not on ingest.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="kind"></param>
        /// <param name="caseSensitive"></param>
        /// <returns></returns>
        public static string ForValue(string s, FieldKind kind, bool caseSensitive = false)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var nfc = s.Normalize(NormalizationForm.FormC).Trim();

            return kind switch
            {
                // URIs are case/char sensitive; do nothing but NFC
                FieldKind.Uri => nfc, 
                // e.g., ESP, DL
                FieldKind.Code => caseSensitive ? nfc.Trim() : nfc.Trim().ToUpperInvariant(),
                // label-friendly for fuzzy/token work
                FieldKind.Label => Regex.Replace(nfc.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim(),
                // light normalization; parsing handled elsewhere
                FieldKind.Numeric => nfc.Replace(',', '.'), 
                // Default
                _ => nfc.Trim()
            };
        }
    }
}