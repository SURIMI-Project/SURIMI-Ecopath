using ControlledVocabularies.Core;
using System;
using System.Collections.Generic;

namespace ControlledVocabularies.Utils
{
    public static class FieldNameHelper
    {
        // keep small, easy to move to LocalSettings later
        private static readonly string[] CodeAffixes = { "code", "id", "key" };
        private static readonly string[] NameAffixes = { "name", "label", "title" };

        // whole-name tokens that by themselves denote a code (normalized)
        private static readonly string[] CodeWholeNames = { "alpha3" };

        public enum AffixPosition { None = 0, Leading, Trailing, Whole }

        /// <summary>
        /// True if field name denotes a code column. Returns the stem (the meaningful part).
        /// </summary>
        public static bool IsCodeField(string fieldName, out string stem)
        {
            stem = string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName)) return false;

            var schema = FieldPolicy.ForSchema(fieldName);

            // Whole-name match (e.g., "alpha3")
            if (CodeWholeNames.Contains(schema))
            {
                stem = schema; // or string.Empty; keeping schema is sometimes handy for cross-matching
                return true;
            }

            // Leading or trailing affix split
            string affix;
            AffixPosition pos;
            if (TrySplitAffix(schema, CodeAffixes, out stem, out affix, out pos))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// True if field name denotes a name/label column. Returns the stem (the meaningful part).
        /// </summary>
        public static bool IsNameField(string fieldName, out string stem)
        {
            stem = string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName)) return false;

            var schema = FieldPolicy.ForSchema(fieldName);

            string affix;
            AffixPosition pos;
            if (TrySplitAffix(schema, NameAffixes, out stem, out affix, out pos))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to find a Code↔Name paired column based on shared stem (order-independent).
        /// </summary>
        public static bool TryFindPairedField(IEnumerable<string> allFields, string fieldName, out string pairedField, out string relation)
        {
            pairedField = string.Empty;
            relation = string.Empty;

            if (IsCodeField(fieldName, out var stem))
            {
                foreach (var other in allFields)
                {
                    if (IsNameField(other, out var stem2) && StringComparer.Ordinal.Equals(stem, stem2))
                    {
                        pairedField = other; relation = "Code↔Name"; return true;
                    }
                }
                return false;
            }

            if (IsNameField(fieldName, out stem))
            {
                foreach (var other in allFields)
                {
                    if (IsCodeField(other, out var stem2) && StringComparer.Ordinal.Equals(stem, stem2))
                    {
                        pairedField = other; relation = "Name↔Code"; return true;
                    }
                }
                return false;
            }

            return false;
        }

        /// <summary>
        /// Split off a leading or trailing affix (with or without '-' / '_') and return stem+affix+position.
        /// </summary>
        public static bool TrySplitAffix(string schemaFieldName, IEnumerable<string> affixCandidates,
                                         out string stem, out string affix, out AffixPosition position)
        {
            stem = string.Empty;
            affix = string.Empty;
            position = AffixPosition.None;

            if (string.IsNullOrWhiteSpace(schemaFieldName)) return false;

            // Try trailing then leading; order is arbitrary, both are supported.
            foreach (var sfx in affixCandidates)
            {
                if (EndsWith(schemaFieldName, sfx))
                {
                    var prefix = schemaFieldName.Substring(0, schemaFieldName.Length - sfx.Length);
                    if (EndsWith(prefix, "-") || EndsWith(prefix, "_"))
                        prefix = prefix.Substring(0, prefix.Length - 1);

                    if (prefix.Length > 0)
                    {
                        stem = prefix;
                        affix = sfx;
                        position = AffixPosition.Trailing;
                        return true;
                    }
                }

                // explicit separator trailing: "...-code" / "..._code"
                if (EndsWith(schemaFieldName, "-" + sfx) || EndsWith(schemaFieldName, "_" + sfx))
                {
                    var prefix = schemaFieldName.Substring(0, schemaFieldName.Length - (sfx.Length + 1));
                    if (prefix.Length > 0)
                    {
                        stem = prefix;
                        affix = sfx;
                        position = AffixPosition.Trailing;
                        return true;
                    }
                }

                // Leading affix: "code-..." / "code_..." / "code..."
                if (StartsWith(schemaFieldName, sfx))
                {
                    var rest = schemaFieldName.Substring(sfx.Length);
                    if (StartsWith(rest, "-") || StartsWith(rest, "_"))
                        rest = rest.Substring(1);

                    if (rest.Length > 0)
                    {
                        stem = rest;
                        affix = sfx;
                        position = AffixPosition.Leading;
                        return true;
                    }
                }

                // explicit separator leading handled by the block above (starts with sfx + '-' or '_')
            }

            return false;
        }

        /// <summary>
        /// Simple tokenizer for schema-like names.
        /// </summary>
        public static void Tokenize(string input, ISet<string> output)
        {
            output.Clear();
            if (string.IsNullOrWhiteSpace(input)) return;

            string s = FieldPolicy.ForSchema(input);
            int i = 0;
            while (i < s.Length)
            {
                while (i < s.Length && !char.IsLetterOrDigit(s[i])) i++;
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '\'')) i++;
                if (i > start)
                {
                    var tok = s.Substring(start, i - start);
                    if (tok.Length > 0) output.Add(tok);
                }
            }
        }

        private static bool EndsWith(string s, string suffix)
        {
            if (suffix.Length > s.Length) return false;
            int offset = s.Length - suffix.Length;
            for (int i = 0; i < suffix.Length; i++)
                if (s[offset + i] != suffix[i]) return false;
            return true;
        }

        private static bool StartsWith(string s, string prefix)
        {
            if (prefix.Length > s.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (s[i] != prefix[i]) return false;
            return true;
        }
    }
}
