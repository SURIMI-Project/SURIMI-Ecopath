using ControlledVocabularies.Core;

namespace ControlledVocabularies.ForeignKeys
{
    public sealed class ForeignKeySpec
    {
        /// <summary>
        ///  e.g. "ASFIS" or alias
        /// </summary>
        public string TargetVocabulary { get; init; } = "";

        /// <summary>
        /// e.g. "alpha3_code"
        /// </summary>
        public string TargetField { get; init; } = "";

        /// <summary>
        /// domain of target vocab
        /// </summary>
        public KeyDomain TargetDomain { get; init; }

        /// <summary>
        /// purpose of that field
        /// </summary>
        public KeyPurpose TargetPurpose { get; init; }

        /// <summary>
        /// if true: only exact
        /// </summary>
        public bool Strict { get; init; } = true;
    }
}