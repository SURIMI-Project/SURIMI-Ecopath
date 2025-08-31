namespace ControlledVocabularies.Match
{
    public sealed class KeywordFieldMatcher : IFieldMatcher
    {
        public double Score(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;

            // Tokenize (very simply; reuse FieldPolicy normalization upstream)
            var aTokens = Tokenize(a);
            if (aTokens.Count == 0) return 0.0;

            var bTokens = Tokenize(b);
            if (bTokens.Count == 0) return 0.0;

            int hits = 0;
            foreach (var tok in aTokens)
                if (bTokens.Contains(tok)) hits++;

            // proportion of a’s unique tokens found in b
            return hits / (double)aTokens.Count;
        }

        private static HashSet<string> Tokenize(string s)
        {
            var tokens = new HashSet<string>(StringComparer.Ordinal);
            int i = 0;
            while (i < s.Length)
            {
                // skip non-alnum
                while (i < s.Length && !char.IsLetterOrDigit(s[i])) i++;
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '\'')) i++;
                if (i > start)
                {
                    var tok = s[start..i].ToLowerInvariant();
                    if (tok.Length > 0) tokens.Add(tok);
                }
            }
            return tokens;
        }
    }
}