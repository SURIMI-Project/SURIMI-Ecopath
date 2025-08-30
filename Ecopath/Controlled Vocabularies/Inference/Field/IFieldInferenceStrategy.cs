using ControlledVocabularies.Core;
using ControlledVocabularies.Context;

namespace ControlledVocabularies.Inference.Field
{
    /// <summary>
    /// Base interface for field analysis strategies
    /// </summary>
    public interface IFieldInferenceStrategy
    {
        string Name { get; }
        double Priority { get; } // Higher priority strategies run first

        FieldInferenceResult Analyze(string fieldName, IEnumerable<string> sampleValues, IEnumerable<MultiLevelKey> allRecords, ModelContext context);
    }
}