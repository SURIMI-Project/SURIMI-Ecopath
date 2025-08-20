using ControlledVocabularies.Core;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Resolve;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Match
{
    public class GenericVocabularyMatcher : IVocabularyMatcher
    {
        public MatchResult Match(MultiLevelKey record, IControlledVocabulary vocabA, IControlledVocabulary vocabB, int? minscore = null)
        {
            MatchResult best = MatchResult.NoMatch;

            if (vocabA == null || vocabB == null || !MatchHelpers.CanMatch(vocabA, vocabB))
                return best;

            if (minscore == null) minscore = LocalSettings.DefaultMinScore;

            // 1. Try FK shortcut
            var fkMatch = TryMatchViaForeignKey(record, vocabA, vocabB);
            if (fkMatch.Score == 100) return fkMatch;

            // 2) Strategy search across compatible fields/strategies
            foreach (string sourceField in record.FieldNames)
            {
                var sourceValue = record.GetField(sourceField)?.ToString(false);
                if (string.IsNullOrWhiteSpace(sourceValue)) continue;

                // if you have any source-side hinting, pass it here; else leave null
                var mappings = BuildMappingsForField(sourceField, vocabB /*, sourceHint: null */);

                var inputKey = MultiLevelKey.FromPairs([(sourceField, sourceValue)], vocabB.Domain, strict: false);

                foreach (var map in mappings)
                {
                    var resolver = new StrategyKeyResolver(vocabB.Records, new[] { map });
                    var match = resolver.FindBestMatch(inputKey); // ensure this returns the max-scoring row

                    if (match != null && match.Score > Math.Max(best.Score, minscore ?? LocalSettings.DefaultMinScore))
                    {
                        match.Justification = $"Matched '{sourceField}' > '{map.TargetField}' via {map.Strategy}";
                        best = match;
                    }
                }
            }
            // 3. Fallback fallback: use brute force???

            return best;
        }

        #region Internal FK Logic

        private MatchResult TryMatchViaForeignKey(MultiLevelKey record, IControlledVocabulary vocabA, IControlledVocabulary vocabB)
        {
            var matcher = new ExactFieldMatcher();
            var vocabBNorm = StringHelpers.NormalizeName(vocabB.VocabularyName);

            foreach (var fieldName in vocabA.FieldNames)
            {
                var spec = vocabA.GetKeyFieldDescriptor(fieldName)?.ForeignKey;
                if (spec == null) continue;

                // Prefer registry alias resolution (if you have it); else normalize and compare:
                var targetVocabNorm = StringHelpers.NormalizeName(spec.TargetVocabulary);
                if (matcher.Score(vocabBNorm, targetVocabNorm) < 1) continue;

                var inputValue = record.GetField(fieldName)?.ToString(false);
                if (string.IsNullOrWhiteSpace(inputValue)) continue;

                // Hint field first
                if (!string.IsNullOrEmpty(spec.TargetField) && vocabB.FieldNames.Contains(spec.TargetField))
                {
                    foreach (var r in vocabB.Records)
                    {
                        if (matcher.Score(r.GetField(spec.TargetField)!.ToString(false), inputValue) == 1)
                        {
                            return new MatchResult
                            {
                                Score = 100,
                                SourceField = fieldName,
                                SourceFieldValue = inputValue,
                                TargetField = spec.TargetField,
                                TargetFieldValue = inputValue,
                                MatchedKey = r,
                                StrategyUsed = MatchStrategy.Exact,
                                Justification = $"Matched via FK hint '{spec.TargetField}'"
                            };
                        }
                    }
                    // log: hint not found
                }

                // Fallback: exact on any compatible target field (still one mapping per resolver)
                var inputKey = MultiLevelKey.FromPairs([(fieldName, inputValue!)], vocabB.Domain, strict: false);
                foreach (var map in BuildMappingsForField(fieldName, vocabB, sourceHint: MatchStrategy.Exact))
                {
                    var resolver = new StrategyKeyResolver(vocabB.Records, new[] { map });
                    var match = resolver.FindBestMatch(inputKey);
                    if (match != null && match.Score > 0)
                    {
                        match.Justification = $"Matched via FK fallback (Exact) on '{map.TargetField}'";
                        return match;
                    }
                }
            }

            return MatchResult.NoMatch;
        }

        #endregion // Internal FK Logic

        #region Internal helpers

        private static IEnumerable<MatchStrategy> EnumerateFlags(MatchStrategy flags)
        {
            foreach (MatchStrategy f in Enum.GetValues(typeof(MatchStrategy)))
                if (f != MatchStrategy.None && flags.HasFlag(f))
                    yield return f;
        }

        private IEnumerable<StrategyKeyResolver.FieldMapping> BuildMappingsForField(string sourceField, IControlledVocabulary vocabB, MatchStrategy? sourceHint = null)
        {
            foreach (string targetField in vocabB.FieldNames)
            {
                var descr = vocabB.GetKeyFieldDescriptor(targetField);
                if (descr == null) continue;

                // If you have a source-side hint, intersect; otherwise just use target flags.
                var effective = sourceHint.HasValue ? (descr.Strategy & sourceHint.Value) : descr.Strategy;
                foreach (var strategy in EnumerateFlags(effective))
                {
                    yield return new StrategyKeyResolver.FieldMapping(sourceField, targetField)
                    {
                        Strategy = strategy,
                        Weight = Math.Max(1, descr.Weight),
                        IsRequired = descr.IsRequired
                    };
                }
            }
        }
        #endregion // Internal helpers
    }
}