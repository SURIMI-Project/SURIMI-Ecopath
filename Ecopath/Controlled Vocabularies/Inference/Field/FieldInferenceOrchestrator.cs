using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Inference.Field.Strategies;
using ControlledVocabularies.Registries;

namespace ControlledVocabularies.Inference.Field
{
    /// <summary>
    /// Orchestrates multiple analysis strategies
    /// </summary>
    public class FieldInferenceOrchestrator
    {
        private readonly List<IFieldInferenceStrategy> _strategies = new();

        public FieldInferenceOrchestrator(IVocabularyRegistry? _registry)
        {
            RegisterStrategy(new CodeNamePairStrategy());
            RegisterStrategy(new HierarchicalStructureStrategy());
            RegisterStrategy(new RepetitiveMeaningfulStrategy());
            RegisterStrategy(new DomainSpecificTermStrategy(_registry));
            RegisterStrategy(new BasicStatisticsStrategy()); // Fallback
        }

        public void RegisterStrategy(IFieldInferenceStrategy strategy) => _strategies.Add(strategy);

        public CompositeFieldInferenceResult AnalyzeField(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context)
        {
            var results = new List<FieldInferenceResult>();

            // Run strategies in priority order
            foreach (var strategy in _strategies.OrderByDescending(s => s.Priority))
            {
                var result = strategy.Analyze(fieldName, sampleValues, allRecords, context);
                if (result.HasSuggestion)
                {
                    results.Add(result);
                }
            }

            return new CompositeFieldInferenceResult(fieldName, results);
        }
    }
}