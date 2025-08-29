using ControlledVocabularies.Context;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference
{
    public sealed class VocabularyAnalysisOrchestrator
    {
        private readonly List<IVocabularyAnalysisStrategy> _strategies = new();

        private sealed class StrategyPriorityComparer : IComparer<IVocabularyAnalysisStrategy>
        {
            public int Compare(IVocabularyAnalysisStrategy? a, IVocabularyAnalysisStrategy? b)
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                // Descending priority
                if (a.Priority > b.Priority) return -1;
                if (a.Priority < b.Priority) return 1;
                return 0;
            }
        }

        public void Register(IVocabularyAnalysisStrategy strategy)
        {
            if (strategy == null) return;
            _strategies.Add(strategy);
        }

        public SemanticInferenceResult Analyze(IControlledVocabulary vocab, ModelContext context, System.Func<string, FieldInferenceInfo> fieldInfoProvider,
            System.Action<SemanticInferenceResult> finalize)
        {
            var result = new SemanticInferenceResult(vocab.VocabularyName);

            _strategies.Sort(new StrategyPriorityComparer());

            int i = 0;
            while (i < _strategies.Count)
            {
                var s = _strategies[i];
                s.Analyze(vocab, context, result, fieldInfoProvider);
                i++;
            }

            if (finalize != null)
            {
                finalize(result); // your rewritten InferPrimarySemantics
            }

            return result;
        }
    }
}