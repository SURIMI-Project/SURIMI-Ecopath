using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Descriptors
{
    /// <summary>
    /// Describes the properties of a multi-level key field, and 
    /// the value content of the field in a <see cref="IControlledVocabulary"/>.
    /// </summary>
    public interface IKeyFieldDescriptor
    {
        /// <summary>
        /// The internal field name.
        /// </summary>
        string FieldName { get; }

        /// <summary>
        /// The <see cref="KeyPurpose">knowledge domain</see> of his this field
        /// </summary>
        KeyDomain Domain { get; }

        /// <summary>
        /// The <see cref="KeyPurpose">purpose</see> of this field
        /// </summary>
        KeyPurpose Purpose { get; }

        /// <summary>
        /// The <see cref="FieldKind"/> of the field.
        /// </summary>
        FieldKind Kind { get; set; }

        /// <summary>
        /// Flag, stating whether this field is mandatory
        /// </summary>
        bool IsRequired { get; }

        /// <summary>
        /// The weight to allocate to field matches [1, 100]
        /// </summary>
        int Weight { get; }

        /// <summary>
        /// Bit flags that identify the most likely matching stratey across vocabularies
        /// </summary>
        MatchStrategy Strategy { get; set; }

        /// <summary>
        /// Average length of values in a vocabulary
        /// </summary>
        int AvgLength { get; set; }

        /// <summary>
        /// Case sensiveness in a vocabulary
        /// </summary>
        bool CaseSensitive { get; init; }

        /// <summary>
        /// Number of disctinct values in a vocabulary
        /// </summary>
        int DistinctValueCount { get; set; }

        /// <summary>
        /// Foreign key specification, if any.
        /// </summary>
        ForeignKeySpec? ForeignKey { get; set; }

        /// <summary>
        /// Non-zero ratio in a vocabulary.
        /// </summary>
        double NonZeroRatio { get; set; }

        /// <summary>
        /// Value uniqueness ratio in a vocabulary.
        /// </summary>
        double UniquenessRatio { get; set; }

        /// <summary>
        /// Flag, indicating of field weights were automatically assigned.
        /// </summary>
        bool UseAutoWeight { get; }

        /// <summary>
        /// User-assigned field weight.
        /// </summary>
        int UserWeight { get; }
    }
}