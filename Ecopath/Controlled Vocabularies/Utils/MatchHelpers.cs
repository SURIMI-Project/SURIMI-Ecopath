using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Utils
{
    public static class MatchHelpers
    {
        /// <summary>
        /// Returns true if two fields are matchable based on domain and purpose.
        /// </summary>
        public static bool CanMatch(KeyFieldDescriptor a, KeyFieldDescriptor b, bool enforceKind = false)
        {
            if (a == null || b == null) return false;
            if (a.Domain != b.Domain) return false;
            if ((a.Purpose & b.Purpose) == 0) return false;

            if (!enforceKind) return true;

            // Unknown acts as wildcard; Code<->Label is usually OK; other mismatches are suspicious
            bool kindOk =
                a.Kind == FieldKind.Unknown || b.Kind == FieldKind.Unknown ||
                a.Kind == b.Kind ||
                (a.Kind, b.Kind) is (FieldKind.Code, FieldKind.Label) or (FieldKind.Label, FieldKind.Code);

            return kindOk;
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

            if (source.Domain != target.Domain)
                return false;

            if ((source.Purpose & target.Purpose) != 0)
                return true;

            //if (source.ForeignKeyMap.Values.Any(v =>
            //    v.StartsWith(target.VocabularyName, StringComparison.OrdinalIgnoreCase)))
            //    return true;

            return false;
        }
    }
}
