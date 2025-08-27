using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Inference;

namespace ControlledVocabularies.Inference
{

    /// <summary>
    /// Enhanced field inference info with importance weighting and hierarchical analysis
    /// </summary>
    public class FieldInferenceInfo
    {
        public string FieldName { get; }
        public FieldImportanceWeight ImportanceWeight { get; set; } = FieldImportanceWeight.Unknown;
        public KeyFieldDescriptor? Descriptor { get; private set; }
        public HierarchicalNestingAnalysis? HierarchicalNesting { get; private set; }

        // ... (existing properties: SemanticHints, IsPotentialForeignKey, etc.)
        public List<SemanticHint> SemanticHints { get; } = new();
        public bool IsPotentialForeignKey { get; set; }
        public double ForeignKeyConfidence { get; set; }
        public List<string> Reasons { get; } = new();
        public List<string> Diagnostics { get; } = new();

        public FieldInferenceInfo(string fieldName) => FieldName = fieldName;

        public void SetDescriptor(KeyFieldDescriptor descriptor) => Descriptor = descriptor;
        public void SetHierarchicalNesting(HierarchicalNestingAnalysis analysis) => HierarchicalNesting = analysis;

        public void AddSemanticHint(KeyDomain domain, KeyPurpose purpose, double confidence, string reason) =>
            SemanticHints.Add(new SemanticHint(domain, purpose, confidence, reason));

        public void AddReason(string reason) => Reasons.Add(reason);
        public void AddDiagnostic(string diagnostic) => Diagnostics.Add(diagnostic);

        /// <summary>
        /// Overall field confidence score incorporating importance, descriptor insights, and hierarchy
        /// </summary>
        public double OverallConfidence
        {
            get
            {
                double confidence = (int)ImportanceWeight / 5.0; // Base from importance

                if (Descriptor != null)
                {
                    confidence += Descriptor.UniquenessRatio * 0.2; // Uniqueness bonus
                    confidence -= (1.0 - Descriptor.NonZeroRatio) * 0.1; // Sparsity penalty
                }

                if (HierarchicalNesting?.IsHierarchical == true)
                {
                    confidence += HierarchicalNesting.ConsistencyRatio * 0.1; // Hierarchy bonus
                }

                return Math.Clamp(confidence, 0.0, 1.0);
            }
        }
    }
}