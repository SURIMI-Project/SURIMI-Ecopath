using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Resolve;

namespace ControlledVocabularies.Match
{
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
            return FindAllMatches(sourceValue, records, descriptor, minScore)
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
            int threshold = minScore ?? LocalSettings.DefaultMinScore;

            // Not strict as we're using an invented field name here
            var inputKey = MultiLevelKey.FromPairs([("value", sourceValue)], descriptor.Domain, strict: false);

            Dictionary<MultiLevelKey, MatchResult> results = new();
            string targetField = descriptor.FieldName;
            int weight = Math.Max(1, descriptor.Weight); // defensive
            bool isRequired = descriptor.IsRequired;
            MatchStrategy finalStrategy = descriptor.Strategy;

            foreach (var strategy in EnumerateFlags(finalStrategy))
            {
                var resolver = new StrategyKeyResolver(
                    records,
                    new[]
                    {
                        new FieldMapping("value", targetField)
                        {
                            Strategy   = strategy,
                            Weight     = weight,
                            IsRequired = isRequired
                        }
                    });

                var matches = resolver.FindAllMatches(inputKey);

                // Normalize per strategy, and make robust to no score
                if (matches is not null && matches.Count() > 0)
                {
                    int maxScore = matches.Max(m => m.Score);
                    double denom = maxScore > 100 ? maxScore : 100.0;

                    foreach (var match in matches)
                    {
                        // Normalize by actual max for this strategy
                        int normalized = (int)Math.Round((match.Score / denom) * 100.0);
                        normalized = Math.Clamp(normalized, 0, 100);

                        if (normalized < threshold) continue;

                        if (!results.TryGetValue(match.MatchedKey, out var existing) || normalized > existing.Score)
                        {
                            results[match.MatchedKey] = new MatchResult
                            {
                                SourceField = match.SourceField,
                                SourceFieldValue = match.SourceFieldValue,
                                TargetField = match.TargetField,
                                TargetFieldValue = match.TargetFieldValue,
                                MatchedKey = match.MatchedKey,
                                Score = normalized,
                                StrategyUsed = strategy,
                                Justification = $"Matched on '{targetField}' via {strategy}"
                            };
                        }
                    }
                }
            }

            return results.Values;
        }

        private static IEnumerable<MatchStrategy> EnumerateFlags(MatchStrategy flags)
        {
            foreach (MatchStrategy s in Enum.GetValues(typeof(MatchStrategy)))
                if (s != MatchStrategy.None && flags.HasFlag(s))
                    yield return s;
        }
    }
}