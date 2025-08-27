using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference
{
    public static class FieldImportanceWeightExtensions
    {
        public static FieldImportanceWeight LowerBySparsity(this FieldImportanceWeight weight) =>
            weight > FieldImportanceWeight.Unknown ? weight - 1 : weight;
    }
}