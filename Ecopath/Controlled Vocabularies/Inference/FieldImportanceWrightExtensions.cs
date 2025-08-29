using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference
{
    public static class FieldImportanceWeightExtensions
    {
        public static FieldImportanceWeight LowerBySparsity(this FieldImportanceWeight w)
        {
            switch (w)
            {
                case FieldImportanceWeight.Name: return FieldImportanceWeight.Context;
                case FieldImportanceWeight.Context: return FieldImportanceWeight.Code;
                case FieldImportanceWeight.Code: return FieldImportanceWeight.Description;
                default: return w; // Description/Unknown stay as-is
            }
        }
    }
}