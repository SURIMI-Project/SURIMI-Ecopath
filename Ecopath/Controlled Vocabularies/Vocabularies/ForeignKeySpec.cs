using ControlledVocabularies.Core;

namespace ControlledVocabularies.Vocabularies
{
    public sealed class ForeignKeySpec
    {
        public string TargetVocabulary { get; init; } = "";   // e.g. "ASFIS" or alias
        public string TargetField { get; init; } = "";   // e.g. "alpha3_code"
        public KeyDomain TargetDomain { get; init; }         // domain of target vocab
        public KeyPurpose TargetPurpose { get; init; }         // purpose of that field
        public bool Strict { get; init; } = true; // if true: only exact
    }
}