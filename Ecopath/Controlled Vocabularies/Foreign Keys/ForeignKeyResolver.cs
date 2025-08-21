using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using ControlledVocabularies.Registries;

namespace ControlledVocabularies.ForeignKeys
{
    public sealed class ForeignKeyResolver : IForeignKeyResolver
    {
        private readonly IVocabularyRegistry _registry;
        private readonly IFieldMatcher _exact = new ExactFieldMatcher();

        public ForeignKeyResolver(IVocabularyRegistry registry) => _registry = registry;

        public MatchResult TryResolve(MultiLevelKey record,
                                      IControlledVocabulary source,
                                      IControlledVocabulary target)
        {
            var targetNorm = StringHelpers.NormalizeName(target.VocabularyName);

            foreach (var fieldName in source.FieldNames)
            {
                var spec = source.GetKeyFieldDescriptor(fieldName)?.ForeignKey ?? null;
                if (spec == null) continue;

                if (!_registry.TryGetByNameOrAlias(spec.TargetVocabulary, out var fkTarget) || fkTarget == null)
                    continue;

                if (_exact.Score(StringHelpers.NormalizeName(fkTarget.VocabularyName), targetNorm) < 1)
                    continue;

                var inputValue = record.GetField(fieldName)?.ToString(false);
                if (string.IsNullOrWhiteSpace(inputValue)) continue;

                // 1) Direct hint on specific target field
                if (!string.IsNullOrEmpty(spec.TargetField) && fkTarget.FieldNames.Contains(spec.TargetField))
                {
                    foreach (var r in fkTarget.Records)
                    {
                        if (_exact.Score(r.GetField(spec.TargetField)!.ToString(false), inputValue) == 1)
                        {
                            return new MatchResult
                            {
                                SourceVocabulary = source.VocabularyName,
                                TargetVocabulary = target.VocabularyName,
                                Score = 100,
                                SourceField = fieldName,
                                SourceFieldValue = inputValue,
                                TargetField = spec.TargetField,
                                TargetFieldValue = inputValue,
                                MatchedKey = r,
                                StrategyUsed = MatchStrategy.Exact,
                                Justification = $"FK '{fieldName}'→{fkTarget.VocabularyName}.{spec.TargetField}"
                            };
                        }
                    }
                }

                // 2) Fallback: exact against any target field
                var inputKey = MultiLevelKey.FromPairs([(fieldName, inputValue!)], fkTarget.Domain, strict: false);
                foreach (var tf in fkTarget.FieldNames)
                {
                    var mapping = new ControlledVocabularies.Resolve.StrategyKeyResolver.FieldMapping(fieldName, tf)
                    {
                        Strategy = MatchStrategy.Exact,
                        Weight = 1
                    };
                    var resolver = new ControlledVocabularies.Resolve.StrategyKeyResolver(fkTarget.Records, new[] { mapping });
                    var match = resolver.FindBestMatch(inputKey);
                    if (match != null && match.Score > 0)
                    {
                        match.SourceVocabulary = source.VocabularyName;
                        match.TargetVocabulary = target.VocabularyName;
                        match.Justification = $"FK fallback (Exact) '{fieldName}'→{fkTarget.VocabularyName}.{tf}";
                        return match;
                    }
                }
            }

            return MatchResult.NoMatch;
        }
    }
}