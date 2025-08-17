public class MultiLevelKeyIndexer 
{
    public bool BuildIndex(string fieldName, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor)
    {
        List<string> values = new();
        int totalRecords = records.Count();

        // Aggregate values per field
        foreach (var record in records)
        {
            var field = record.GetField(fieldName);
            if (field == null) return false;

            string value = field.Value?.Trim() ?? String.Empty;
            if (!string.IsNullOrWhiteSpace(value))
                values.Add(value);
        }

        int nonZero = values.Count;
        int distinct = values.Distinct().Count();
        double nonZeroRatio = totalRecords > 0 ? (double)nonZero / totalRecords : 0.0;
        double uniquenessRatio = nonZero > 0 ? (double)distinct / nonZero : 0.0;
        int avgLen = nonZero > 0 ? (int)values.Average(v => v.Length) : 0;

        descriptor.AvgLength = avgLen;
        descriptor.DistinctValueCount = distinct;
        descriptor.NonZeroRatio = nonZeroRatio;
        descriptor.UniquenessRatio = uniquenessRatio;
        descriptor.Strategies = InferStrategies(values, avgLen, distinct, uniquenessRatio, nonZeroRatio);

        return true;
    }

    /// <summary>
    /// Infer a field intercomparison strategy based on a set of simple rules
    /// related to field length, content, and content uniqueness.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="avgLen"></param>
    /// <param name="distinct"></param>
    /// <param name="uniquenessRatio"></param>
    /// <param name="nonZeroRatio"></param>
    /// <returns></returns>
    private static MatchStrategy InferStrategies(List<string> values, int avgLen, int distinct, double uniquenessRatio, double nonZeroRatio)
    {
        // Very sparse fields: skip outright
        if (nonZeroRatio < 0.1)
            return MatchStrategy.DontBother;

        // Quick structural signals
        bool mostlyNumeric = values.All(v => v.All(char.IsDigit));
        double upperRatio = UppercaseRatio(values);         // 0..1
        double avgWordCount = AverageWordCount(values);       // ~0 for codes
        bool looksLikeUri = values.Any(LooksLikeUriOrDoi);  // any URI/DOI present?

        // URIs/DOIs: almost never worth cross-vocab matching
        if (looksLikeUri)
            return MatchStrategy.DontBother;

        MatchStrategy strategy = MatchStrategy.None;

        // Short, uppercase, highly-unique fields => codes (e.g., ASFIS alpha3)
        // thresholds: len<=5, upper>=0.9, unique>=0.7, words<=1.1
        if (avgLen <= 5 && upperRatio >= 0.9 && uniquenessRatio >= 0.7 && avgWordCount <= 1.1)
            strategy |= MatchStrategy.Exact;

        // Medium-length labels: allow exact + fuzzy for robust name matching
        if (avgLen > 5 && avgLen <= 25)
        {
            strategy |= MatchStrategy.Exact;
            if (!mostlyNumeric)
                strategy |= MatchStrategy.Fuzzy;
        }

        // Long text or very high distinct count → keyword/token overlap
        if (avgLen > 25 || distinct > 100)
            strategy |= MatchStrategy.Keyword | MatchStrategy.TokenOverlap;

        // Pure numeric fields: enable numeric-range semantics (optional downstream)
        if (mostlyNumeric)
            strategy |= MatchStrategy.NumericRange;

        // If nothing triggered, fall back to Exact for safety on mid/short labels
        if (strategy == MatchStrategy.None && avgLen > 0 && avgLen <= 25)
            strategy |= MatchStrategy.Exact;

        return strategy;
    }

    // --- helpers ---

    private static double UppercaseRatio(List<string> values)
    {
        if (values.Count == 0) return 0.0;
        int upperish = 0;
        foreach (var v in values)
        {
            // consider A–Z and digits/underscores as "code-friendly"
            bool ok = v.All(ch => char.IsUpper(ch) || char.IsDigit(ch) || ch == '_' || ch == '-');
            if (ok) upperish++;
        }
        return (double)upperish / values.Count;
    }

    private static double AverageWordCount(List<string> values)
    {
        if (values.Count == 0) return 0.0;
        double sum = 0;
        foreach (var v in values)
        {
            // split on whitespace; treat empty as 0
            var wc = string.IsNullOrWhiteSpace(v) ? 0 : v.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            sum += wc;
        }
        return sum / values.Count;
    }

    private static bool LooksLikeUriOrDoi(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();

        // very lightweight checks to avoid regex overhead unless needed
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("doi:", StringComparison.OrdinalIgnoreCase))
            return true;

        // optional: compact regex for http(s) or doi (keep simple to avoid false positives)
        // return Regex.IsMatch(s, @"^(https?://|doi:)", RegexOptions.IgnoreCase);

        return false;
    }

}
