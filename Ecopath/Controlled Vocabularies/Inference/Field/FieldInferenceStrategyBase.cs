using ControlledVocabularies.Context;
using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference.Field
{
    public abstract class FieldInferenceStrategyBase : IFieldInferenceStrategy
    {
        public abstract string Name { get; }

        public double Priority { get; set;}

        public abstract FieldInferenceResult Analyze(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context);
    }
}
