using ControlledVocabularies.Core;

namespace ControlledVocabularies.Inference
{
    public record SemanticHint(KeyDomain Domain, KeyPurpose Purpose, double Confidence, string Reason);

}
