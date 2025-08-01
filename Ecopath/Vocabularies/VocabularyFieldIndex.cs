public class FieldProfile
{
    public string FieldName { get; set; }
    public int AvgLength { get; set; }
    public int DistinctValueCount { get; set; }
    public double UniquenessRatio { get; set; }
    public double NonZeroRatio { get; set; }
    public MatchStrategy InferredStrategies { get; set; } = MatchStrategy.None;
}

public class VocabularyFieldIndex : IVocabularyFieldIndex
{
    public Dictionary<string, FieldProfile> Profiles { get; set; } = new();

    public static VocabularyFieldIndex FromData(IEnumerable<MultiLevelKey> records)
    {
        var index = new VocabularyFieldIndex();
        var fieldStats = new Dictionary<string, List<string>>();
        int totalRecords = records.Count();

        // Aggregate values per field
        foreach (var record in records)
        {
            foreach (string fieldName in record.FieldNames)
            {
                var field = record.GetField(fieldName);
                if (field == null) continue;

                string value = field.Value?.Trim();
                if (!fieldStats.ContainsKey(fieldName))
                    fieldStats[fieldName] = new List<string>();

                if (!string.IsNullOrWhiteSpace(value))
                    fieldStats[fieldName].Add(value);
            }
        }

        foreach (var kvp in fieldStats)
        {
            string fieldName = kvp.Key;
            List<string> values = kvp.Value;

            int nonZero = values.Count;
            int distinct = values.Distinct().Count();
            double nonZeroRatio = totalRecords > 0 ? (double)nonZero / totalRecords : 0.0;
            double uniquenessRatio = nonZero > 0 ? (double)distinct / nonZero : 0.0;
            int avgLen = nonZero > 0 ? (int)values.Average(v => v.Length) : 0;

            var profile = new FieldProfile
            {
                FieldName = fieldName,
                AvgLength = avgLen,
                DistinctValueCount = distinct,
                NonZeroRatio = nonZeroRatio,
                UniquenessRatio = uniquenessRatio,
                InferredStrategies = InferStrategies(values, avgLen, distinct, uniquenessRatio, nonZeroRatio)
            };

            index.Profiles[fieldName] = profile;
        }

        return index;
    }

    private static MatchStrategy InferStrategies(List<string> values, int avgLen, int distinct, double uniquenessRatio, double nonZeroRatio)
    {
        if (nonZeroRatio < 0.1)
            return MatchStrategy.DontBother;

        bool mostlyNumeric = values.All(v => v.All(char.IsDigit));
        bool hasColons = values.Any(v => v.Contains(":"));

        MatchStrategy strategy = MatchStrategy.None;

        if (hasColons)
            strategy |= MatchStrategy.ForeignKey;

        if (avgLen <= 4 && uniquenessRatio > 0.8)
            strategy |= MatchStrategy.Exact;

        if (avgLen > 4 && avgLen <= 25)
        {
            strategy |= MatchStrategy.Exact;
            if (!mostlyNumeric)
                strategy |= MatchStrategy.Fuzzy;
        }

        if (avgLen > 25 || distinct > 100)
            strategy |= MatchStrategy.Keyword | MatchStrategy.TokenOverlap;

        if (mostlyNumeric)
            strategy |= MatchStrategy.NumericRange;

        return strategy;
    }

    public FieldProfile? GetProfile(string fieldName)
    {
        Profiles.TryGetValue(fieldName, out var profile);
        return profile;
    }
}
