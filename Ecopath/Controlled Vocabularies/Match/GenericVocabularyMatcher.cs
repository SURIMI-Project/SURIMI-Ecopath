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
            if (fkMatch.Score == 100)
                return fkMatch;

            // 2. Fallback: Use StrategyBasedMatcher
            StrategyBasedMatcher matcher = new();
            foreach (string sourceField in record.FieldNames)
            {
                var sourceValue = record.GetField(sourceField)?.ToString(false);
                if (string.IsNullOrWhiteSpace(sourceValue))
                    continue;

                IEnumerable<StrategyKeyResolver.FieldMapping> mappings = FindCompatibleMappings(sourceField, MatchStrategy.Exact, vocabB);
                if (mappings.Count() > 0)
                {
                    var inputKey = new MultiLevelKey();
                    inputKey.SetField(sourceField, sourceValue);

                    var resolver = new StrategyKeyResolver(vocabB.Records, mappings);
                    var match = resolver.FindBestMatch(inputKey, vocabB.KeyDomain);

                    if (match != null && match.Score > Math.Max(best.Score, (int)minscore))
                    {
                        best = match;
                        match.Justification = $"Matched on field '{sourceField}' using strategy {match.StrategyUsed}";
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
            string vocabBName = StringHelpers.NormalizeName(vocabB.VocabularyName); // The API already does this, but it doesn't hurt to be cautious

            foreach (var (localField, foreignSpec) in vocabA.ForeignKeyMap)
            {
                var inputValue = record.GetField(localField)?.ToString(false);
                if (string.IsNullOrEmpty(inputValue))
                    continue;

                var tokens = foreignSpec.Split(':', StringSplitOptions.RemoveEmptyEntries);
                string foreignVocabName = StringHelpers.NormalizeName(tokens[0]);
                string foreignFieldHint = tokens.Length > 1 ? tokens[1] : string.Empty;

                // Not a referenced vocabulary?
                // ToDo: here vocabulary aliases should be considered
                if (matcher.Score(vocabBName, foreignVocabName) < 1)
                    continue;

                // Try specified FK field if hint is available
                if (!string.IsNullOrEmpty(foreignFieldHint) && vocabB.FieldNames.Contains(foreignFieldHint))
                {
                    foreach (var r in vocabB.Records)
                    {
                        if (matcher.Score(r.GetField(foreignFieldHint)!.ToString(false), inputValue) == 1)
                        {
                            return new MatchResult()
                            {
                                Score = 100,
                                SourceField = localField,
                                SourceFieldValue = inputValue,
                                TargetField = foreignFieldHint,
                                TargetFieldValue = inputValue,
                                MatchedKey = r,
                                StrategyUsed = MatchStrategy.Exact,
                                Justification = $"Matched via FK hint {foreignFieldHint}"
                            };
                        }
                    }
                    Console.WriteLine($"Warning: FK hint '{foreignFieldHint}' not found in vocab {vocabB.VocabularyName}");
                }

                // Fallback: Try any field with MatchStrategy.Exact
                IEnumerable<StrategyKeyResolver.FieldMapping> mappings = FindCompatibleMappings(localField, MatchStrategy.Exact, vocabB);
                if (mappings.Any())
                {
                    var inputKey = new MultiLevelKey();
                    inputKey.SetField(localField, inputValue);

                    var resolver = new StrategyKeyResolver(vocabB.Records, mappings);
                    var match = resolver.FindBestMatch(inputKey, vocabB.KeyDomain);

                    if (match != null && match.Score > 0)
                    {
                        match.Justification = $"Matched via FK fallback using strategy Exact on all compatible fields. See details";
                        return match;
                    }
                }
            }

            return MatchResult.NoMatch;
        }

        #endregion // Internal FK Logic

        #region Internal helpers

        private IEnumerable<StrategyKeyResolver.FieldMapping> FindCompatibleMappings(string localField, MatchStrategy strategySource, IControlledVocabulary vocab)
        {
            List<StrategyKeyResolver.FieldMapping> mappings = new();
            foreach (string fn in vocab.FieldNames)
            {
                var descrDest = vocab.GetKeyFieldDescriptor(fn);
                foreach (MatchStrategy strategy in Enum.GetValues(typeof(MatchStrategy)))
                {
                    if ((strategy & strategySource) > 0 && (strategy & descrDest!.Strategy) > 0)
                    {
                        mappings.Add(new StrategyKeyResolver.FieldMapping(localField, fn)
                        {
                            Weight = 1,
                            IsRequired = false,
                            Strategy = strategy
                        });
                    }
                }
            }
            return mappings;
        }

        #endregion // Internal helpers
    }
}