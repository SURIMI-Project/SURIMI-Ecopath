using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Inference.Field.Strategies
{
    /// <summary>
    /// Fallback strategy using basic statistical analysis
    /// </summary>
    public class BasicStatisticsStrategy : IFieldInferenceStrategy
    {
        public string Name => "BasicStatistics";
        public double Priority => 1.0; // Lowest priority - fallback only

        public FieldInferenceResult Analyze(string fieldName, List<string> sampleValues,
            IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var result = new FieldInferenceResult { StrategyName = Name };

            if (!sampleValues.Any()) return result;

            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);
            var avgLength = sampleValues.Average(v => v.Length);
            var uniqueness = sampleValues.Distinct().Count() / (double)sampleValues.Count;
            var uppercaseRatio = CalculateUppercaseRatio(sampleValues);
            var allNumeric = sampleValues.All(v => double.TryParse(v, out _));
            var hasUriPattern = sampleValues.Any(v => v.StartsWith("http") || v.Contains("://"));

            // Basic kind inference
            FieldKind suggestedKind;
            if (hasUriPattern)
                suggestedKind = FieldKind.Uri;
            else if (allNumeric)
                suggestedKind = FieldKind.Numeric;
            else if ((avgLength <= 6 || normalizedFieldName.Contains("code")) && uppercaseRatio >= 0.7 && uniqueness >= 0.7)
                suggestedKind = FieldKind.Code;
            else
                suggestedKind = FieldKind.Label;

            // Basic strategy inference
            var suggestedStrategy = suggestedKind switch
            {
                FieldKind.Code => MatchStrategy.Exact,
                FieldKind.Label => MatchStrategy.Exact | MatchStrategy.Fuzzy,
                FieldKind.Uri => MatchStrategy.Exact,
                FieldKind.Numeric => MatchStrategy.Exact,
                _ => MatchStrategy.Exact
            };

            result.SuggestedKind = suggestedKind;
            result.SuggestedStrategy = suggestedStrategy;
            result.SuggestedWeight = 3; // Default weight
            result.Confidence = 0.3; // Low confidence - this is fallback logic
            result.Evidence.Add($"Basic statistical analysis: avg length={avgLength:F1}, uniqueness={uniqueness:P1}, uppercase={uppercaseRatio:P1}");

            return result;
        }

        private double CalculateUppercaseRatio(List<string> values)
        {
            if (!values.Any()) return 0;

            var uppercaseCount = values.Count(v =>
                v.All(c => char.IsUpper(c) || char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)));

            return (double)uppercaseCount / values.Count;
        }
    }
}