using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Analysis;
using ControlledVocabularies.Analysis.Strategies;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Descriptors
{
    /// <summary>
    /// Strategy-based field indexer with pluggable analysis strategies
    /// </summary>
    /// <todo>Add configuration for strategy enablement/priority</todo>
    /// <todo>Add async strategy execution for heavy analysis</todo>
    /// <todo>Add strategy result caching for performance</todo>
    public class KeyFieldIndexer : IKeyFieldIndexer
    {
        private readonly FieldAnalysisOrchestrator _orchestrator;
        private readonly IVocabularyRegistry? _registry;

        public KeyFieldIndexer(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
            _orchestrator = new FieldAnalysisOrchestrator();
            RegisterDefaultStrategies();
        }

        private void RegisterDefaultStrategies()
        {
            // Register strategies in priority order - high priority = more reliable
            _orchestrator.RegisterStrategy(new CodeNamePairStrategy());
            _orchestrator.RegisterStrategy(new HierarchicalStructureStrategy());
            _orchestrator.RegisterStrategy(new RepetitiveMeaningfulStrategy());
            _orchestrator.RegisterStrategy(new DomainSpecificTermStrategy(_registry));
            _orchestrator.RegisterStrategy(new BasicStatisticsStrategy()); // Always run as fallback
        }

        public bool BuildIndex(string fieldName, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor)
        {
            fieldName = FieldPolicy.ForSchema(fieldName); // Ensure normalization

            var allRecords = records.ToList();
            if (!allRecords.Any()) return false;

            // Extract sample values
            var sampleValues = ExtractFieldValues(allRecords, fieldName);
            if (!sampleValues.Any()) return false;

            // Build analysis context
            var context = new AnalysisContext
            {
                VocabularyDomain = KeyDomain.NotSet, // Will be inferred
                VocabularyPurpose = KeyPurpose.NotSet, // Will be inferred
                Registry = _registry,
                ExistingDescriptor = descriptor
            };

            // Run strategy-based analysis
            var analysisResult = _orchestrator.AnalyzeField(fieldName, sampleValues, allRecords, context);

            // Apply recommendations to descriptor
            ApplyAnalysisToDescriptor(analysisResult, descriptor);

            // Populate basic statistics for compatibility
            PopulateBasicStatistics(sampleValues, allRecords, descriptor);

            return true;
        }

        /// <summary>
        /// Extract field values with consistent normalization
        /// </summary>
        private List<string> ExtractFieldValues(List<MultiLevelKey> records, string fieldName, int maxSamples = 200)
        {
            var values = new List<string>();
            var seen = new HashSet<string>();

            foreach (var record in records.Take(maxSamples * 2)) // Sample more for diversity
            {
                var field = record.GetField(fieldName);
                var value = field?.Value?.Trim();

                if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                {
                    values.Add(value);
                    if (values.Count >= maxSamples) break;
                }
            }

            return values;
        }

        /// <summary>
        /// Apply strategy analysis results to field descriptor
        /// </summary>
        private void ApplyAnalysisToDescriptor(CompositeAnalysisResult analysisResult, KeyFieldDescriptor descriptor)
        {
            // Apply kind recommendation
            if (analysisResult.RecommendedKind != FieldKind.Unknown)
            {
                descriptor.Kind = analysisResult.RecommendedKind;
            }

            // Apply strategy recommendation
            if (analysisResult.RecommendedStrategy != MatchStrategy.None)
            {
                descriptor.Strategy = analysisResult.RecommendedStrategy;
            }

            // Apply weight recommendation
            if (descriptor.UseAutoWeight && analysisResult.RecommendedWeight > 0)
            {
                descriptor.AutoWeight = Math.Clamp(analysisResult.RecommendedWeight, 1, 10);
            }

            // Store analysis metadata for debugging
            /// <todo>Add analysis metadata storage for debugging/diagnostics</todo>
        }

        /// <summary>
        /// Populate basic statistics for backward compatibility
        /// </summary>
        private void PopulateBasicStatistics(List<string> values, List<MultiLevelKey> records, KeyFieldDescriptor descriptor)
        {
            var totalRecords = records.Count;
            var nonZeroCount = values.Count;
            var distinctCount = values.Distinct().Count();

            descriptor.AvgLength = nonZeroCount > 0 ? (int)values.Average(v => v.Length) : 0;
            descriptor.DistinctValueCount = distinctCount;
            descriptor.NonZeroRatio = totalRecords > 0 ? (double)nonZeroCount / totalRecords : 0.0;
            descriptor.UniquenessRatio = nonZeroCount > 0 ? (double)distinctCount / nonZeroCount : 0.0;
        }
    }
}