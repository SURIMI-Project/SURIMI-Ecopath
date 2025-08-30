using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;

namespace ControlledVocabularies.Descriptors
{
     /// <todo>Finalize FieldKind + CaseSensitive; default CaseSensitive=true for Uri.</todo>
    /// <todo>Add Seal() to prevent post-load edits (Strategy/AutoWeight) and enforce at runtime.</todo>
    /// <todo>Consider unit/scale metadata for Numeric fields to enable NumericRange matching.</todo>
    public class KeyFieldDescriptor : IKeyFieldDescriptor
    {
        /// <inheritdocs/>
        /// <param name="fieldName"></param>
        /// <param name="domain">The knowledge domain this field adheres to.</param>
        /// <param name="purpose">The </param>
        /// <param name="kind">The function of this field.</param>
        /// <param name="isRequired"></param>
        /// <param name="weight">[1, 10]. Set to 0 (default) to have an indexer cacluclate weights automatically.</param>
        /// <param name="strategy">Matching strategy, "exact" by default.</param>
        public KeyFieldDescriptor(string fieldName, KeyDomain domain, KeyPurpose purpose, FieldKind kind,
            bool isRequired = false, int weight = 0, MatchStrategy strategy = MatchStrategy.Exact)
        {
            FieldName = fieldName;
            Kind = kind;
            Domain = domain;
            Purpose = purpose;
            IsRequired = isRequired;
            UserWeight = weight; // 0 = auto
            Strategy = strategy;
            IsIndexed = false;
        }

        /// <inheritdoc/>
        public string FieldName { get; }

        /// <inheritdoc/>
        public bool IsRequired { get; }

        /// <summary>
        /// Get/set the <see cref="FieldKind">function of the field</see>.
        /// </summary>
        public FieldKind Kind { get; set; } = FieldKind.Unknown;

        public bool UseAutoKind { get; set; } = true;

        /// <summary>
        /// Get/set the <see cref="KeyDomain"/> that this field adheres to.
        /// </summary>
        public KeyDomain Domain { get; }

        /// <summary>
        /// Get/set the <see cref="KeyPurpose"/> that discribes how this field should be interpreted.
        /// </summary>
        public KeyPurpose Purpose { get; }

        /// <summary>
        /// Get/set the <see cref="MatchStrategy"> to parse this field (correlated with <see cref="Kind"/>).
        /// </summary>
        public MatchStrategy Strategy { get; set; }
        public bool UseAutoStrategy { get; set; } = true;

        public bool UseAutoWeight => (UserWeight == 0);
        internal int? AutoWeight { get; set; } // set by indexer
        public int Weight => Math.Clamp(UseAutoWeight ? (AutoWeight ?? 1) : UserWeight, 1, 100);

        public int UserWeight { get; }

        public ForeignKeySpec? ForeignKey { get; set; }  // null if not an FK

        public int AvgLength { get; set; }
        public int DistinctValueCount { get; set; }
        public double UniquenessRatio { get; set; }
        public double NonZeroRatio { get; set; }

        public bool CaseSensitive { get; init; } = false; // opt-in (true for URIs, most codes remain false)

        public bool IsIndexed { get; set; } 


        public override string ToString() => $"{Domain}.{FieldName} ({Purpose}) w={Weight} [{Strategy}]";
    }
}