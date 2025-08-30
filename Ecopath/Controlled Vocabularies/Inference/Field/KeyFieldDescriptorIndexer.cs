using ControlledVocabularies.Common;
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Inference.Field.Strategies;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Inference.Field
{
    public class KeyFieldDescriptorIndexer : IKeyFieldDescriptorIndexer
    {
        private readonly FieldInferenceOrchestrator _orchestrator;
        private readonly IVocabularyRegistry? _registry;

        public KeyFieldDescriptorIndexer(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
            _orchestrator = new FieldInferenceOrchestrator();
            RegisterDefaultStrategies();
        }

        private void RegisterDefaultStrategies()
        {
            _orchestrator.RegisterStrategy(new CodeNamePairStrategy());
            _orchestrator.RegisterStrategy(new HierarchicalStructureStrategy());
            _orchestrator.RegisterStrategy(new RepetitiveMeaningfulStrategy());
            _orchestrator.RegisterStrategy(new DomainSpecificTermStrategy(_registry));
            _orchestrator.RegisterStrategy(new BasicStatisticsStrategy());
        }

        public bool BuildIndex(string fieldName, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor)
        {
            if (descriptor.IsIndexed)
                return true;

            fieldName = FieldPolicy.ForSchema(fieldName);

            // materialize without LINQ
            if (records.Count() == 0) return false;

            var samples = FieldFilter.ExtractFieldValues(records, fieldName);
            if (samples.Count == 0) return false;

            var context = GlobalServiceLocator.Get<ModelContext>() ?? ModelContext.Empty;
            var analysis = _orchestrator.AnalyzeField(fieldName, samples, records, context);

            ApplyAnalysisToDescriptor(analysis, descriptor);
            PopulateBasicStatistics(samples, records, descriptor);
            return true;
        }

        private void ApplyAnalysisToDescriptor(CompositeFieldInferenceResult result, KeyFieldDescriptor descriptor)
        {
            if (result.RecommendedKind != FieldKind.Unknown)
                descriptor.Kind = result.RecommendedKind;

            if (result.RecommendedStrategy != MatchStrategy.None)
                descriptor.Strategy = result.RecommendedStrategy;

            // Always record AutoWeight (clamped); UserWeight==0 means UseAutoWeight=true
            descriptor.AutoWeight = Math.Clamp(result.RecommendedWeight, 1, 10);
            descriptor.IsIndexed = true;
        }

        private void PopulateBasicStatistics(IEnumerable<string> values, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor)
        {
            int totalRecords = records.Count();
            int nonZeroCount = values.Count();

            int totalLen = 0;
            foreach (var value in values) totalLen += value.Length;
            descriptor.AvgLength = nonZeroCount > 0 ? (int)(totalLen / (double)nonZeroCount) : 0;

            var distinct = new HashSet<string>();
            foreach (var value in values) distinct.Add(value);
            int distinctCount = distinct.Count;

            descriptor.DistinctValueCount = distinctCount;
            descriptor.NonZeroRatio = totalRecords > 0 ? (double)nonZeroCount / totalRecords : 0.0;
            descriptor.UniquenessRatio = nonZeroCount > 0 ? (double)distinctCount / nonZeroCount : 0.0;
        }
    }
}
