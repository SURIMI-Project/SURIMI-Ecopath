using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Match;

namespace ControlledVocabularies.Resolve
{
    public partial class StrategyKeyResolver : IKeyResolver
    {
        protected readonly IEnumerable<MultiLevelKey> TargetValues;
        protected readonly List<FieldMapping> FieldMappings = new();
        public bool NormalizeScores { get; set; } = true;

        public StrategyKeyResolver(IEnumerable<MultiLevelKey> targetValues, IEnumerable<KeyFieldDescriptor> descriptors)
        {
            TargetValues = targetValues;
            foreach (KeyFieldDescriptor descr in descriptors)
                FieldMappings.Add(new FieldMapping(descr.FieldName, descr.FieldName)
                {
                    Weight = descr.Weight,
                    IsRequired = descr.IsRequired,
                    Strategy = descr.Strategy
                });
        }

        public StrategyKeyResolver(IEnumerable<MultiLevelKey> targetValues, IEnumerable<FieldMapping> mappings)
        {
            TargetValues = targetValues;
            FieldMappings.AddRange(mappings);
        }

        public IEnumerable<KeyResolverMatchResult> FindAllMatches(MultiLevelKey input, KeyDomain domain, int? minscore = null)
        {
            if (minscore == null) minscore = LocalSettings.DefaultMinScore;

            List<KeyResolverMatchResult> matches = new();

            foreach (MultiLevelKey candidate in TargetValues.Where(k => k.Domain == domain))
            {
                var match = MatchScore(input, candidate);
                if (match.Score > 0)
                    matches.Add(match);
            }

            if (NormalizeScores && matches.Count > 0)
            {
                int maxScore = matches.Max(m => m.Score);
                if (maxScore > 0 && maxScore > 100)
                {
                    foreach (var match in matches)
                        match.Score = (int)(match.Score * (100.0 / maxScore));
                }
            }

            return matches.Where(p => p.Score >= minscore);
        }

        public KeyResolverMatchResult? FindBestMatch(MultiLevelKey input, KeyDomain domain, int? minscore = null)
        {
            return FindAllMatches(input, domain, minscore)
                .OrderByDescending(m => m.Score)
                .FirstOrDefault();
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
                            Justification = "Field match"
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