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
            fieldName = FieldPolicy.ForSchema(fieldName);

            // materialize without LINQ
            var all = new List<MultiLevelKey>();
            foreach (var r in records) all.Add(r);
            if (all.Count == 0) return false;

            var samples = FieldFilter.ExtractFieldValues(all, fieldName);
            if (samples.Count == 0) return false;

            var context = GlobalServiceLocator.Get<ModelContext>() ?? ModelContext.Empty;
            var analysis = _orchestrator.AnalyzeField(fieldName, samples, all, context);

            ApplyAnalysisToDescriptor(analysis, descriptor);
            PopulateBasicStatistics(samples, all, descriptor);
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
        }

        private void PopulateBasicStatistics(List<string> values, List<MultiLevelKey> records, KeyFieldDescriptor descriptor)
        {
            int totalRecords = records.Count;
            int nonZeroCount = values.Count;

            int totalLen = 0;
            for (int i = 0; i < values.Count; i++) totalLen += values[i].Length;
            descriptor.AvgLength = nonZeroCount > 0 ? (int)(totalLen / (double)nonZeroCount) : 0;

            var distinct = new HashSet<string>();
            for (int i = 0; i < values.Count; i++) distinct.Add(values[i]);
            int distinctCount = distinct.Count;

            descriptor.DistinctValueCount = distinctCount;
            descriptor.NonZeroRatio = totalRecords > 0 ? (double)nonZeroCount / totalRecords : 0.0;
            descriptor.UniquenessRatio = nonZeroCount > 0 ? (double)distinctCount / nonZeroCount : 0.0;
        }
    }
}
