using FuzzySharp;
using System.Text;
using System.Text.RegularExpressions;

namespace Utilities
{
    public class NameUtilities
    {
        public static string NormalizeName(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return string.Empty;

            // Normalize to Unicode NFC
            string normalized = str.Normalize(NormalizationForm.FormC);
            // Lowercase invariant
            string invariant = normalized.ToLowerInvariant();
            // Split by regex: std whitespace \s and unicode whitespaces \p{Z}
            string[] bits = Regex.Split(invariant.Trim(), @"[\s\p{Z}]+");
            // Join 'em up again with regular spaces
            return string.Join(" ", bits);
        }

        public static (string BestMatch, int Score) FuzzyMatch(string input, IEnumerable<string> knownNames, int minScore = 80)
        {
            string normInput = NormalizeName(input);

            var best = knownNames
                .Select(name => new
                {
                    Name = name,
                    Score = Fuzz.Ratio(normInput, NormalizeName(name))
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return best != null && best.Score >= minScore
                ? (best.Name, best.Score)
                : (string.Empty, best != null ? best.Score : 0);
        }
    }
}
