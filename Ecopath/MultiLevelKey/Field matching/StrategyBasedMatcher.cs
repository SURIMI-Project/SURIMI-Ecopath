/// <summary>
/// Strategy-based resolver to resolve a single source value to target field in a collectiopn of records.
/// </summary>
public class StrategyBasedMatcher
{
    /// <summary>
    /// Find the best match of a value in a range of records, based on the <see cref="MatchStrategy"/> attached to the target field
    /// </summary>
    /// <param name="sourceValue">The value to look up.</param>
    /// <param name="records">The records to look through.</param>
    /// <param name="descriptor">The field descriptor that describes the target field to search</param>
    /// <param name="minScore">The scoring threshold.</param>
    /// <returns></returns>
    public MatchResult? FindBestMatch(string sourceValue, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor, int? minScore = null)
    {
        return FindAllMatches(sourceValue,  records, descriptor, minScore)
            .OrderByDescending(m => m.Score)
            .FirstOrDefault();
    }

    /// <summary>
    /// Find all matches of a value in a range of records, based on the <see cref="MatchStrategy"/> attached to the target field.
    /// </summary>
    /// <param name="sourceValue">The value to look up.</param>
    /// <param name="records">The records to look through.</param>
    /// <param name="descriptor">The field descriptor that describes the target field to search</param>
    /// <param name="minScore">The scoring threshold.</param>
    /// <returns></returns>
    public IEnumerable<MatchResult> FindAllMatches(string sourceValue, IEnumerable<MultiLevelKey> records, KeyFieldDescriptor descriptor, int? minScore = null)
    {
        if (minScore == null) minScore = LocalSettings.DefaultMinScore;

        var inputKey = new MultiLevelKey();
        inputKey.SetField("value", sourceValue);

        string targetField = descriptor.FieldName;
        int weight = descriptor.Weight;
        bool isRequired = descriptor.IsRequired;
        MatchStrategy finalStrategy = descriptor.Strategy;

        Dictionary<MultiLevelKey, MatchResult> results = new();

        foreach (MatchStrategy strategy in Enum.GetValues(typeof(MatchStrategy)))
        {
            if (!finalStrategy.HasFlag(strategy) || strategy == MatchStrategy.None)
                continue;

            var resolver = new StrategyKeyResolver(
                records,
                [
                    new StrategyKeyResolver.FieldMapping("value", targetField)
                    {
                        Strategy = strategy,
                        Weight = weight,
                        IsRequired = isRequired
                    }
                ]);

            foreach (var match in resolver.FindAllMatches(inputKey, descriptor.Domain))
            {
                double actualScore = match.Score;
                int realScore = Math.Clamp((int)actualScore * 100, 0, 100);

                if (realScore >= minScore)
                {
                    if (!results.TryGetValue(match.MatchedKey, out var existing) || match.Score > existing.Score)
                    {
                        results[match.MatchedKey] = match;
                    }
                }
            }
        }
        return results.Values;
    }
}