using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Inference.Field.Strategies
{

    /// <summary>
    /// Domain-specific term detection using vocabulary matching
    /// </summary>
    public class DomainSpecificTermStrategy : IFieldInferenceStrategy
    {
        private readonly IVocabularyRegistry? _registry;
        private readonly GenericVocabularyMatcher _vocabMatcher;

        public DomainSpecificTermStrategy(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
            _vocabMatcher = new GenericVocabularyMatcher(_registry);
        }
        public string Name => "DomainSpecific";
        public double Priority => 1.0; // Lowest priority - fallback only

        public FieldInferenceResult Analyze(string fieldName, List<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var result = new FieldInferenceResult { StrategyName = Name };

            if (_registry == null) return result;

            // Test field name against domain-specific vocabularies using matchers
            var domainConfidence = TestFieldNameAgainstDomainVocabularies(fieldName);

            // Test sample values against vocabularies
            var contentConfidence = TestSampleValuesAgainstVocabularies(fieldName, sampleValues);

            if (domainConfidence > 0.5 || contentConfidence > 0.5)
            {
                result.Confidence = Math.Max(domainConfidence, contentConfidence);
                result.SuggestedKind = FieldKind.Code; // Domain terms usually indicate codes
                result.Evidence.Add($"Domain matching: field name confidence {domainConfidence:F2}, content confidence {contentConfidence:F2}");
            }

            return result;
        }

        private double TestFieldNameAgainstDomainVocabularies(string fieldName)
        {
            var normalizedFieldName = FieldPolicy.ForSchema(fieldName);
            double bestScore = 0;

            // Test against all vocabulary field names using your existing matcher infrastructure
            foreach (var vocab in _registry!.GetAll())
            {
                foreach (var vocabFieldName in vocab.FieldNames)
                {
                    var normalizedVocabField = FieldPolicy.ForSchema(vocabFieldName);

                    // Use fuzzy matcher for field name similarity
                    var score = new FuzzyFieldMatcher().Score(normalizedFieldName, normalizedVocabField);
                    bestScore = Math.Max(bestScore, score);
                }
            }

            return bestScore;
        }

        private double TestSampleValuesAgainstVocabularies(string fieldName, List<string> sampleValues)
        {
            if (!sampleValues.Any()) return 0;

            var testKey = MultiLevelKey.FromPairs([(fieldName, sampleValues.First())], KeyDomain.NotSet);
            double bestScore = 0;

            foreach (var vocab in _registry!.GetAll())
            {
                // Use your existing vocabulary matcher - ultimate API consistency!
                var result = _vocabMatcher.Match(testKey, vocab, vocab);
                bestScore = Math.Max(bestScore, result.Score / 100.0); // Normalize to 0-1
            }

            return bestScore;
        }
    }
}