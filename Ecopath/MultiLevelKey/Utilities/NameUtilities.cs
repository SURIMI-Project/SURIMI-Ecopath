using FuzzySharp;
using System.Text;
using System.Text.RegularExpressions;

namespace Utilities
{
    public class NameUtilities
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

        /// <summary>
        /// Returns the best fuzzy match for a string against a collection
        /// </summary>
        /// <param name="input"></param>
        /// <param name="knownNames"></param>
        /// <param name="minScore"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Chop in different word combinations in case fuzzy matches are unsuccessful.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="maxN"></param>
        /// <returns></returns>
        private static List<string> GenerateNGrams(string input, int maxN = 3)
        {
            var tokens = NormalizeName(input).Split(' ');
            var ngrams = new List<string>();

            for (int n = 1; n <= Math.Min(maxN, tokens.Length); n++)
            {
                for (int i = 0; i <= tokens.Length - n; i++)
                {
                    ngrams.Add(string.Join(" ", tokens.Skip(i).Take(n)));
                }
            }

            return ngrams;
        }

        /// <summary>
        /// Performs a fuzzy match of words within a collection
        /// </summary>
        /// <param name="input"></param>
        /// <param name="knownNames"></param>
        /// <param name="minScore"></param>
        /// <returns></returns>
        public static (string BestMatch, int Score) TokenSetFuzzyMatch(string input, IEnumerable<string> knownNames, int minScore = 80)
        {
            string normInput = NormalizeName(input);

            var best = knownNames
                .Select(name => new
                {
                    Name = name,
                    Score = Fuzz.TokenSetRatio(normInput, NormalizeName(name))
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return best != null && best.Score >= minScore
                ? (best.Name, best.Score)
                : (string.Empty, best != null ? best.Score : 0);
        }

        public static (string BestMatch, int Score) TokenSetFuzzyMatchFallback(string input, IEnumerable<string> knownNames, int minScore = 80)
        {
            // Step 1: Try full string match using token set logic
            var direct = TokenSetFuzzyMatch(input, knownNames, minScore);
            if (!string.IsNullOrEmpty(direct.BestMatch))
                return direct;

            // Step 2: Generate n-grams and try each against known names
            var ngrams = GenerateNGrams(input);
            var best = ngrams
                .Select(ng => TokenSetFuzzyMatch(ng, knownNames, minScore: 70))  // Lower minScore for partials
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return best.Score >= minScore ? best : (string.Empty, 0);
        }
    }
}
