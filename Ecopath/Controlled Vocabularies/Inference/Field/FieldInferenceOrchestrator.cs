using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Inference.Field.Strategies;

namespace ControlledVocabularies.Inference.Field
{
    /// <summary>
    /// Orchestrates multiple analysis strategies
    /// </summary>
    public class FieldInferenceOrchestrator
    {
        private readonly List<IFieldInferenceStrategy> _strategies = new();

        public FieldInferenceOrchestrator()
        {
            // Register strategies in priority order
            RegisterStrategy(new CodeNamePairStrategy());
            RegisterStrategy(new HierarchicalStructureStrategy());
            RegisterStrategy(new RepetitiveMeaningfulStrategy());
            RegisterStrategy(new BasicStatisticsStrategy()); // Fallback
                                                             // ... more strategies
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