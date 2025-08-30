using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Inference.Field.Strategies
{
    /// <summary>
    /// Detects repetitive but meaningful patterns (like gear codes that repeat across records)
    /// </summary>
    public class RepetitiveMeaningfulStrategy : IFieldInferenceStrategy
    {
        private readonly ContainsFieldMatcher _containsMatcher = new();

        public string Name => "RepetitiveMeaningful";
        public double Priority => 7.0;

        public FieldInferenceResult Analyze(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var result = new FieldInferenceResult { StrategyName = Name };

            if (sampleValues.Count() < 5) return result; // Need sufficient sample

            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);
            var uniqueValues = sampleValues.Distinct();
            var uniquenessRatio = (double)uniqueValues.Count() / sampleValues.Count();

            // Check for repetitive pattern (low uniqueness)
            var isRepetitive = uniquenessRatio < 0.3 && uniqueValues.Count() > 1;

            if (isRepetitive)
            {
                // Check if field name suggests meaningful categorization
                var meaningfulTerms = new[] { "gear", "category", "type", "group", "class" };
                var isMeaningful = meaningfulTerms.Any(term =>
                    _containsMatcher.Score(FieldPolicy.ForSchema(term), normalizedFieldName) > 0);

                // Check if field name suggests it's a code
                var isCodeField = _containsMatcher.Score(FieldPolicy.ForSchema("code"), normalizedFieldName) > 0;

                if (isMeaningful && isCodeField)
                {
                    result.SuggestedKind = FieldKind.Code;
                    result.SuggestedStrategy = MatchStrategy.Exact | MatchStrategy.ForeignKey;
                    result.SuggestedWeight = 7;
                    result.Confidence = Math.Min(0.85, 0.5 + (1 - uniquenessRatio)); // Higher confidence for more repetition
                    result.Evidence.Add($"Repetitive meaningful pattern: {uniquenessRatio:P1} uniqueness ({uniqueValues.Count()} unique values in {sampleValues.Count()} records)");
                    result.Evidence.Add($"Field name suggests categorization: {string.Join(", ", meaningfulTerms.Where(term => _containsMatcher.Score(FieldPolicy.ForSchema(term), normalizedFieldName) > 0))}");
                    result.Evidence.Add("Likely classification code with foreign key potential");
                    result.Metadata["UniquenessRatio"] = uniquenessRatio;
                    result.Metadata["UniqueValueCount"] = uniqueValues.Count();
                }
            }

            return result;
        }
    }
}