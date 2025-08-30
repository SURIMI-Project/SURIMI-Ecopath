using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Vocabulary
{
    public interface IVocabularyInferenceStrategy
    {
        string Name { get; }
        double Priority { get; }  // higher runs first

        VocabularyStrategyResult Analyze( IControlledVocabulary vocabulary, ModelContext? modelContext, IVocabularyRegistry? registry);
    }
}
