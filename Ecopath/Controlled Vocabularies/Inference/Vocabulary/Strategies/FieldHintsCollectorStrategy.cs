using ControlledVocabularies.Context;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Strategies
{
    public sealed class FieldHintsCollectorStrategy : IVocabularyAnalysisStrategy
    {
        public string Name => "FieldHintsCollector";
        public double Priority => 8.5;

        public void Analyze( IControlledVocabulary vocab, ModelContext context, SemanticInferenceResult acc, System.Func<string, FieldInferenceInfo> fieldInfoProvider)
        {
            var fields = vocab.FieldNames.GetEnumerator();
            while (fields.MoveNext())
            {
                var fname = fields.Current;
                var info = fieldInfoProvider(fname);
                if (info != null)
                {
                    acc.AddFieldInference(info);
                }
            }
        }
    }
}
