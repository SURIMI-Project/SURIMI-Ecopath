using ControlledVocabularies.Core;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.ForeignKeys
{
    /// <summary>
    /// Definition of a foreign key from one <see cref="IControlledVocabulary"/> to another.
    /// The target voabulary is indicated by name/alias and a target field hint.
    /// </summary>
    /// <todo>Keep ForeignKeySpec immutable (init-only); equality already implemented—add tests for null/strict cases.</todo>
    /// <todo>Surface GetForeignKeyFieldNames/GetForeignKey/TryGetForeignKey/GetForeignKeys for testability & tooling.</todo>
    /// <todo>Decide on behavior when setting a FK to a non-existent target field (throw vs return false); document.</todo>
    public sealed class ForeignKeySpec : IEquatable<ForeignKeySpec>
    {
        private string m_targetVocabulary = "";
        private string m_targetField = "";

        public ForeignKeySpec() 
        { 
        }

        /// <summary>
        /// Get the target vocabulary in <see cref="FieldPolicy.ForSchema(string)">
        /// schema-ready</see> form
        /// </summary>
        public string TargetVocabulary
        {
            get => m_targetVocabulary;
            init => m_targetVocabulary = FieldPolicy.ForSchema(value ?? "");
        }

        /// <summary>
        /// Get the target field in <see cref="FieldPolicy.ForSchema(string)">
        /// schema-ready</see> form
        /// </summary>
        public string TargetField
        {
            get => m_targetField;
            init => m_targetField = FieldPolicy.ForSchema(value ?? "");
        }

        public KeyDomain TargetDomain { get; init; }
        public KeyPurpose TargetPurpose { get; init; }
        public bool Strict { get; init; } = true;

        public override string ToString()
            => $"{TargetVocabulary}.{TargetField} [{TargetDomain}/{TargetPurpose}] strict={Strict}";

        #region IEquatable

        public bool Equals(ForeignKeySpec? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(TargetVocabulary, other.TargetVocabulary, StringComparison.Ordinal)
                && string.Equals(TargetField, other.TargetField, StringComparison.Ordinal)
                && TargetDomain == other.TargetDomain
                && TargetPurpose == other.TargetPurpose
                && Strict == other.Strict;
        }

        public override bool Equals(object? obj) => obj is ForeignKeySpec other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(TargetVocabulary, TargetField, TargetDomain, TargetPurpose, Strict);

        public static bool operator ==(ForeignKeySpec? left, ForeignKeySpec? right)
            => Equals(left, right);

        public static bool operator !=(ForeignKeySpec? left, ForeignKeySpec? right)
            => !Equals(left, right);

        /// <summary>
        /// Convenience: equality ignoring <see cref="Strict"/> (useful for idempotence checks that don’t care about strictness).
        /// </summary>
        public bool SameTargetIgnoringStrict(ForeignKeySpec? other)
        {
            if (other is null) return false;
            return string.Equals(TargetVocabulary, other.TargetVocabulary, StringComparison.Ordinal)
                && string.Equals(TargetField, other.TargetField, StringComparison.Ordinal)
                && TargetDomain == other.TargetDomain
                && TargetPurpose == other.TargetPurpose;
        }

        #endregion // IEuqatable
    }
}