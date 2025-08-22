using ControlledVocabularies.Core;
using ControlledVocabularies.Match;

namespace ControlledVocabularies.Resolve
{
    /// <summary>
    /// Describes a mapping between a source and target field, along with how to compare them.
    /// </summary>
    public class FieldMapping
    {
        public FieldMapping(string sourceField, string targetField)
        {
            SourceField = sourceField ?? throw new ArgumentNullException(nameof(sourceField));
            TargetField = targetField ?? throw new ArgumentNullException(nameof(targetField));
        }

        /// <summary>
        /// The name of the field in the source key.
        /// </summary>
        public string SourceField { get; set; }

        /// <summary>
        /// The name of the field in the target key.
        /// </summary>
        public string TargetField { get; set; }

        /// <summary>
        /// Whether this field is required for a match.
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// The weight to assign to this field match (1-100).
        /// </summary>
        public int Weight { get; set; } = 1;

        public FieldKind Kind { get; set; } = FieldKind.Unknown;
        public bool CaseSensitive { get; set; } = false; // optional, defaults to false

        /// <summary>
        /// The strategy to use for matching this field pair.
        /// </summary>
        public MatchStrategy Strategy { get; set; } = MatchStrategy.Exact;

        /// <summary>
        /// Optional override matcher to use (e.g., a custom fuzzy matcher).
        /// If null, the default for the Strategy will be used.
        /// </summary>
        public IFieldMatcher? Matcher { get; set; }

        public override string ToString()
        {
            return $"{SourceField} -> {TargetField}";
        }
    }

}