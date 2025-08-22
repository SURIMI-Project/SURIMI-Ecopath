using ControlledVocabularies.Core;
using FuzzySharp;

namespace ControlledVocabularies.Utils
{
    public static class FuzzyHelpers
    {
        private const float penaltyFactor = 0.1f;

        /// <summary>
        /// Returns normalized fuzzy score [0.0–1.0] between two strings using standard ratio.
        /// </summary>
        public static double FuzzyRatio(string input, string compare)
        {
            string normInput = FieldPolicy.ForValue(input, FieldKind.Label);
            string normCompare = FieldPolicy.ForValue(compare, FieldKind.Label);

            int rawScore = Fuzz.Ratio(normInput, normCompare);
            return rawScore / 100.0;
        }

        /// <summary>
        /// Returns normalized fuzzy score [0.0–1.0] using token set ratio with noise penalty.
        /// </summary>
        public static double TokenSetFuzzyRatio(string input, string compare)
        {
            string normInput = FieldPolicy.ForValue(input, FieldKind.Label);
            string normCompare = FieldPolicy.ForValue(compare, FieldKind.Label);

            int rawScore = Fuzz.TokenSetRatio(normInput, normCompare);

            var inputTokens = new HashSet<string>(normInput.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var compareTokens = new HashSet<string>(normCompare.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            int shared = inputTokens.Intersect(compareTokens).Count();
            int noise = Math.Max(compareTokens.Count - shared, 0);
            double noiseRatio = compareTokens.Count > 0 ? (double)noise / compareTokens.Count : 0.0;

            double adjustedScore = rawScore * (1.0 - noiseRatio * penaltyFactor);
            return Math.Clamp(adjustedScore / 100.0, 0.0, 1.0);
        }

        /// <summary>
        /// Returns best match and score in [0.0–1.0] using token set logic.
        /// </summary>
        public static (string BestMatch, double Score) BestTokenSetMatch(string input, IEnumerable<string> knownNames)
        {
            string normInput = FieldPolicy.ForValue(input, FieldKind.Label);

            var best = knownNames
                .Select(name => new
                {
                    Name = name,
                    Score = TokenSetFuzzyRatio(input, name)
                })
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return best != null
                ? (best.Name, best.Score)
                : (string.Empty, 0.0);
        }

        /// <summary>
        /// Tries full match first, then partial n-gram fallback.
        /// </summary>
        public static (string BestMatch, double Score) TokenSetFuzzyMatchWithFallback(string input, IEnumerable<string> knownNames)
        {
            input = FieldPolicy.ForValue(input, FieldKind.Label);

            var direct = BestTokenSetMatch(input, knownNames);
            if (direct.Score >= 0.5) // Can tune this
                return direct;

            var ngrams = GenerateNGrams(input);
            var best = ngrams
                .Select(ng => BestTokenSetMatch(ng, knownNames))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            return best.Score > 0.0 ? best : (string.Empty, 0.0);
        }

        #region Internals

        private static IEnumerable<string> GenerateNGrams(string input, int maxN = 3)
        {
            var tokens =input.Split(' ');
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

        #endregion
    }
}
