using ControlledVocabularies.Common;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;

namespace ControlledVocabularies.Resolve
{
    /// <summary>
    /// 
    /// </summary>
    /// <todo>Normalize values only for Exact using FieldKind; keep fuzzy/token matchers label-style.</todo>
    /// <todo>Add NumericRangeFieldMatcher and wire to MatchStrategy.NumericRange.</todo>
    /// <todo>Introduce cancellation for long scans; consider early-exit when perfect (100) match is found.</todo>
    /// <todo>Expose per-field contribution in results for explainability (already partially via Matches list).</todo>
    public class StrategyKeyResolver : IKeyResolver
    {
        protected readonly IEnumerable<MultiLevelKey> TargetValues;
        protected readonly List<FieldMapping> FieldMappings = new();
        public bool NormalizeScores { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetValues"></param>
        /// <param name="descriptors"></param>
        public StrategyKeyResolver(IEnumerable<MultiLevelKey> targetValues, IEnumerable<KeyFieldDescriptor> descriptors)
        {
            TargetValues = targetValues;
            foreach (KeyFieldDescriptor descr in descriptors)
                FieldMappings.Add(new FieldMapping(descr.FieldName, descr.FieldName)
                {
                    Weight = descr.Weight,
                    IsRequired = descr.IsRequired,
                    Strategy = descr.Strategy,
                    Kind = descr.Kind == FieldKind.Unknown ? FieldKind.Label : descr.Kind,
                    CaseSensitive = descr.CaseSensitive
                });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="targetValues"></param>
        /// <param name="mappings"></param>
        public StrategyKeyResolver(IEnumerable<MultiLevelKey> targetValues, IEnumerable<FieldMapping> mappings)
        {
            TargetValues = targetValues;
            FieldMappings.AddRange(mappings);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="minScore"></param>
        /// <returns></returns>
        public IEnumerable<KeyResolverMatchResult> FindAllMatches(MultiLevelKey input, int? minScore = null)
        {
            var results = new List<KeyResolverMatchResult>();
            var threshold = minScore ?? LocalSettings.DefaultMinScore;

            foreach (var candidate in TargetValues)
            {
                var r = MatchScore(input, candidate);
                if (r.Score >= threshold)
                    results.Add(r);
            }

            if (results.Count == 0) 
                return results.ToArray();

            // Stable, deterministic ordering: Score desc, then canonical key asc
            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.MatchedKey?.ToString(), StringComparer.Ordinal);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="minScore"></param>
        /// <returns></returns>
        public KeyResolverMatchResult? FindBestMatch(MultiLevelKey input, int? minScore = null)
        {
            KeyResolverMatchResult? best = null;
            var threshold = minScore ?? LocalSettings.DefaultMinScore;

            foreach (var candidate in TargetValues)
            {
                var r = MatchScore(input, candidate);
                if (r.Score < threshold) continue;

                if (best == null ||
                    r.Score > best.Score ||
                    (r.Score == best.Score && StringComparer.Ordinal.Compare(r.MatchedKey?.ToString(), best.MatchedKey?.ToString()) < 0))
                {
                    best = r;
                }
            }

            return best;
        }

        /// <summary>
        /// Compare two records by compatible fields, and return the total.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private KeyResolverMatchResult MatchScore(MultiLevelKey source, MultiLevelKey target)
        {
            int total = 0;
            List<MatchResult> hits = new();

            foreach (var map in FieldMappings)
            {
                var src = source.GetField(map.SourceField);
                var tgt = target.GetField(map.TargetField);

                if (map.IsRequired && (src == null || tgt == null))
                    return KeyResolverMatchResult.NoMatch;

                if (src != null && tgt != null)
                {
                    string srcVal = src.Value?.Trim() ?? string.Empty;
                    string tgtVal = tgt.Value?.Trim() ?? string.Empty;

                    // For fuzzy/token overlap leave values as-is; those matchers already do label-like normalization internally.
                    if (map.Strategy == MatchStrategy.Exact)
                    {
                        var kind = map.Kind == FieldKind.Unknown ? FieldKind.Label : map.Kind;
                        srcVal = FieldPolicy.ForValue(srcVal, kind, map.CaseSensitive);
                        tgtVal = FieldPolicy.ForValue(tgtVal, kind, map.CaseSensitive);
                    }

                    var matcher = GetMatcherForField(map); // polymorphic
                    double similarity = matcher.Score(srcVal, tgtVal) * 100;

                    if (similarity > 0)
                    {
                        int score = (int)(map.Weight * similarity);
                        hits.Add(new MatchResult()
                        {
                            MatchedKey = target,
                            SourceField = map.SourceField,
                            SourceFieldValue = srcVal,
                            TargetField = map.TargetField,
                            TargetFieldValue = tgtVal,
                            StrategyUsed = map.Strategy,
                            Justification = "Field match",
                            Score = score
                        });
                        total += score;

                    }
                }
            }

            return new KeyResolverMatchResult(hits, total)
            {
                MatchedKey = target,
                Justification = "Mapped field hits; see details"
            };
        }

        protected IFieldMatcher GetMatcherForField(FieldMapping map)
        {
            return map.Matcher ?? MatchStrategyFactory.Create(map.Strategy);
        }

    }
}