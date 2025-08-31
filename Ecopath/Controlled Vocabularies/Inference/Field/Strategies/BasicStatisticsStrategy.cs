using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Utils;
using System.Linq;

namespace ControlledVocabularies.Inference.Field.Strategies
{
    /// <summary>
    /// Fallback strategy using basic statistical analysis. Thresholds come from LocalSettings.
    /// </summary>
    public class BasicStatisticsStrategy : IFieldInferenceStrategy
    {
        public string Name => "BasicStatistics";
        public double Priority => 1.0;

        public FieldInferenceResult Analyze(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var result = new FieldInferenceResult { StrategyName = Name };
            var samples = sampleValues as IList<string> ?? sampleValues.ToList();
            if (samples.Count == 0) return result;

            double avgLength = samples.Average(v => v.Length);
            double uniqueness = samples.Distinct().Count() / (double)samples.Count;
            double uppercaseRatio = CalculateUppercaseRatio(samples);
            bool allNumeric = samples.All(v => double.TryParse(v, out _));
            bool hasUri = samples.Any(v => v.StartsWith("http", StringComparison.OrdinalIgnoreCase) || v.Contains("://"));
            bool hasWhitespace = HasWhitespace(samples);

            bool looksCodeByName = FieldNameHelper.IsCodeField(fieldName, out _);

            var maxShort = LocalSettings.Heuristics_MaxShortLabel;
            var maxMedium = LocalSettings.Heuristics_MaxMediumLabel;
            var maxCodeLen = LocalSettings.Heuristics_MaxLikelyCodeLen;
            var minUpperForCode = LocalSettings.Heuristics_MinUppercaseRatioForCode;
            var minUniqueForCode = LocalSettings.Heuristics_MinUniquenessForCode;
            bool keywordOn = LocalSettings.Heuristics_EnableKeywordStrategy;

            FieldKind kind = FieldKind.Unknown;
            MatchStrategy strategy = MatchStrategy.None;
            int weight = 5;
            double conf = 0.30;

            if (hasUri)
            {
                kind = FieldKind.Uri;
                strategy = MatchStrategy.Exact;
                weight = 5;
                conf = 0.30;
            }
            else if (allNumeric)
            {
                kind = looksCodeByName ? FieldKind.Code : FieldKind.Numeric;
                strategy = MatchStrategy.Exact;
                weight = looksCodeByName ? 10 : 4;
                conf = looksCodeByName ? 1.0 : 0.60;
            }
            else
            {
                bool codeShape = avgLength <= maxCodeLen && uppercaseRatio >= minUpperForCode && uniqueness >= minUniqueForCode && !hasWhitespace;

                if (looksCodeByName || codeShape)
                {
                    kind = FieldKind.Code;
                    strategy = MatchStrategy.Exact;
                    weight = 10;
                    conf = looksCodeByName ? 1.0 : 0.70;
                }
                else
                {
                    kind = FieldKind.Label;

                    if (avgLength <= maxShort)
                    {
                        strategy = MatchStrategy.Exact | MatchStrategy.Fuzzy;
                        weight = 6;
                        conf = 0.60;
                    }
                    else if (avgLength <= maxMedium)
                    {
                        strategy = keywordOn
                            ? MatchStrategy.Fuzzy | MatchStrategy.Keyword
                            : MatchStrategy.Fuzzy;
                        weight = keywordOn ? 5 : 5;
                        conf = 0.60;
                    }
                    else
                    {
                        strategy = keywordOn
                            ? MatchStrategy.Keyword | MatchStrategy.TokenOverlap
                            : MatchStrategy.TokenOverlap;
                        weight = keywordOn ? 3 : 3;
                        conf = 0.60;
                    }
                }
            }

            result.SuggestedKind = kind;
            result.SuggestedStrategy = strategy;
            result.SuggestedWeight = weight;
            result.Confidence = conf;
            result.Evidence.Add($"stats: avg={avgLength:F1}, uniq={uniqueness:P1}, upper={uppercaseRatio:P1}, white={(hasWhitespace ? "y" : "n")}, numeric={(allNumeric ? "y" : "n")}");

            return result;
        }

        private static double CalculateUppercaseRatio(IEnumerable<string> values)
        {
            int total = 0, uppers = 0;
            foreach (var v in values)
            {
                if (string.IsNullOrEmpty(v)) continue;
                total++;
                bool ok = true;
                for (int i = 0; i < v.Length; i++)
                {
                    char c = v[i];
                    if (char.IsLetter(c) && char.IsLower(c)) { ok = false; break; }
                }
                if (ok) uppers++;
            }
            if (total == 0) return 0.0;
            return uppers / (double)total;
        }

        private static bool HasWhitespace(IEnumerable<string> values)
        {
            foreach (var v in values)
            {
                if (string.IsNullOrEmpty(v)) continue;
                for (int i = 0; i < v.Length; i++)
                {
                    if (char.IsWhiteSpace(v[i])) return true;
                }
            }
            return false;
        }
    }
}
