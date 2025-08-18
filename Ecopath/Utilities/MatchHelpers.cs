namespace Ecopath.Utilities
{
    public static class MatchHelpers
    {
        /// <summary>
        /// Returns true if two fields are matchable based on domain and purpose.
        /// </summary>
        public static bool CanMatch(KeyFieldDescriptor a, KeyFieldDescriptor b)
        {
            if (a == null || b == null)
                return false;

            // 1. Must have same domain
            if (a.Domain != b.Domain)
                return false;

            // 2. Must have compatible purpose (bitwise)
            return (a.Purpose & b.Purpose) != 0;
        }

        /// <summary>
        /// Returns true if two vocabularies are considered compatible.
        /// </summary>
        public static bool CanMatch(IControlledVocabulary source, IControlledVocabulary target)
        {
            if (source == null || target == null)
                return false;

            if (source == target)
                return false;

            if (source.KeyDomain != target.KeyDomain)
                return false;

            if ((source.KeyPurpose & target.KeyPurpose) != 0)
                return true;

            if (source.ForeignKeyMap.Values.Any(v =>
                v.StartsWith(target.VocabularyName, StringComparison.OrdinalIgnoreCase)))
                return true;

            return false;
        }
    }
}
