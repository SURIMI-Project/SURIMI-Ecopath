using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Inference.Field.Strategies
{
    /// <summary>
    /// Detects CODE/NAME field pairs using consistent matcher-based comparisons
    /// </summary>
    public class CodeNamePairStrategy : IFieldInferenceStrategy
    {
        private readonly ExactFieldMatcher _exactMatcher = new();
        private readonly ContainsFieldMatcher _containsMatcher = new();

        public string Name => "CodeNamePair";
        public double Priority => 9.0; // Highest priority - most reliable

        public FieldInferenceResult Analyze(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var result = new FieldInferenceResult { StrategyName = Name };
            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);

            // Get all normalized field names for consistent comparison
            var allNormalizedFields = allRecords.SelectMany(r => r.FieldNames)
                .Select(f => FieldPolicy.ForSchema(f))
                .Distinct()
                .ToHashSet();

            // Test for CODE field with corresponding NAME field
            if (ContainsTermUsingMatcher(normalizedFieldName, "code"))
            {
                var correspondingNameField = FindCorrespondingFieldUsingMatchers(
                    normalizedFieldName, allNormalizedFields, "code", "name");

                if (correspondingNameField != null)
                {
                    result.SuggestedKind = FieldKind.Code;
                    result.SuggestedStrategy = MatchStrategy.Exact;
                    result.SuggestedWeight = 8;
                    result.Confidence = 0.95;
                    result.Evidence.Add($"CODE field with corresponding NAME field detected");
                    result.Evidence.Add($"Corresponding field: {correspondingNameField}");
                    return result;
                }
            }

            // Test for NAME field with corresponding CODE field
            if (ContainsTermUsingMatcher(normalizedFieldName, "name") &&
                !ContainsTermUsingMatcher(normalizedFieldName, "code"))
            {
                var correspondingCodeField = FindCorrespondingFieldUsingMatchers(
                    normalizedFieldName, allNormalizedFields, "name", "code");

                if (correspondingCodeField != null)
                {
                    result.SuggestedKind = FieldKind.Label;
                    result.SuggestedStrategy = MatchStrategy.Exact | MatchStrategy.Fuzzy | MatchStrategy.TokenOverlap;
                    result.SuggestedWeight = 6;
                    result.Confidence = 0.9;
                    result.Evidence.Add($"NAME field with corresponding CODE field detected");
                    result.Evidence.Add($"Corresponding field: {correspondingCodeField}");
                }
            }

            return result;
        }

        private bool ContainsTermUsingMatcher(string fieldName, string term)
        {
            var normalizedTerm = FieldPolicy.ForSchema(term);
            return _containsMatcher.Score(normalizedTerm, fieldName) > 0;
        }

        private string? FindCorrespondingFieldUsingMatchers(string fieldName, HashSet<string> allFields,
            string suffix1, string suffix2)
        {
            var normalizedSuffix1 = FieldPolicy.ForSchema(suffix1);
            var normalizedSuffix2 = FieldPolicy.ForSchema(suffix2);

            // Check if field ends with suffix1
            if (!fieldName.EndsWith(normalizedSuffix1)) return null;

            // Extract base name and construct target name
            var baseName = fieldName.Substring(0, fieldName.Length - normalizedSuffix1.Length);
            var targetName = baseName + normalizedSuffix2;

            // Use exact matcher for correspondence detection
            return allFields.FirstOrDefault(f => _exactMatcher.Score(targetName, f) > 0);
        }
    }
}