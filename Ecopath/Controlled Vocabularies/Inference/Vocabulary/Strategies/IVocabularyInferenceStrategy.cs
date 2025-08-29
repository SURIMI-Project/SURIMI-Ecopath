using ControlledVocabularies.Context;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference
{
    public interface IVocabularyAnalysisStrategy
    {
        string Name { get; }
        double Priority { get; } // higher runs first

        // fieldInfoProvider gives access to the per-field inferences you already compute
        void Analyze( IControlledVocabulary vocab, ModelContext context, SemanticInferenceResult accumulator, System.Func<string, FieldInferenceInfo> fieldInfoProvider);
    }
}