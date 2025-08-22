using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;

namespace ControlledVocabularies.Descriptors
{
    /// <summary>
    /// Describes the parsing properties of multi-level key fields.
    /// </summary>
    /// <todo>Finalize FieldKind + CaseSensitive; default CaseSensitive=true for Uri.</todo>
    /// <todo>Add Seal() to prevent post-load edits (Strategy/AutoWeight) and enforce at runtime.</todo>
    /// <todo>Consider unit/scale metadata for Numeric fields to enable NumericRange matching.</todo>
    public class KeyFieldDescriptor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="isRequired"></param>
        /// <param name="weight">[1, 100]. Set to 0 to have an indexer cacluclate weights automatically.</param>
        /// <param name="domain"></param>
        /// <param name="purpose"></param>
        /// <param name="strategy">Matching strategy, "exact" by default.</param>
        public KeyFieldDescriptor(string fieldName, KeyDomain domain, KeyPurpose purpose, bool isRequired = false, int weight = 1, MatchStrategy strategy = MatchStrategy.Exact)
        {
            Domain = domain;
            FieldName = fieldName;
            IsRequired = isRequired;
            UserWeight = weight; // 0 = auto
            Purpose = purpose;
            Strategy = strategy;
        }

        /// <summary>
        /// The internal field name.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Flag, stating whether this field is mandatory
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// The weight to allocate to field matches [1, 100]
        /// </summary>
        public int UserWeight { get; }

        /// <summary>
        /// The purpose of this field
        /// </summary>
        public KeyPurpose Purpose { get; }

        /// <summary>
        /// The knowledge domain this field is obtained from
        /// </summary>
        public KeyDomain Domain { get; }

        /// <summary>
        /// Bit flags that identify the most likely matching stratey for matching across vocabularies
        /// </summary>
        public MatchStrategy Strategy { get; set; }

        public ForeignKeySpec? ForeignKey { get; set; }  // null if not an FK

        public int AvgLength { get; set; }
        public int DistinctValueCount { get; set; }
        public double UniquenessRatio { get; set; }
        public double NonZeroRatio { get; set; }

        public FieldKind Kind { get; set; } = FieldKind.Unknown;
        public bool CaseSensitive { get; init; } = false; // opt-in (true for URIs, most codes remain false)

        public bool UseAutoWeight => (UserWeight == 0);
        internal int? AutoWeight { get; set; } // set by indexer

        public int Weight => Math.Clamp(UseAutoWeight ? (AutoWeight ?? 1) : UserWeight, 1, 100);

        public override string ToString() => $"{Domain}.{FieldName} ({Purpose}) w={Weight} [{Strategy}]";
    }
}