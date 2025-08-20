using ControlledVocabularies.Core;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Descriptors
{
    /// <summary>
    /// Describes the parsing properties of multi-level key fields.
    /// </summary>
    public class KeyFieldDescriptor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="isRequired"></param>
        /// <param name="weight">[1, 100]</param>
        /// <param name="domain"></param>
        /// <param name="purpose"></param>
        /// <param name="strategy">Matching strategy, "exact" by default.</param>
        public KeyFieldDescriptor(string fieldName, KeyDomain domain, KeyPurpose purpose, bool isRequired = false, int weight = 1, MatchStrategy strategy = MatchStrategy.Exact)
        {
            FieldName = fieldName;
            IsRequired = isRequired;
            Weight = weight;
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
        public int Weight { get; }

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

        public ForeignKeySpec? ForeignKey { get; init; }  // null if not an FK

        public int AvgLength { get; set; }
        public int DistinctValueCount { get; set; }
        public double UniquenessRatio { get; set; }
        public double NonZeroRatio { get; set; }
    }
}