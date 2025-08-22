using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using ControlledVocabularies.Registries;

namespace ControlledVocabularies.ForeignKeys
{
    /// <summary>
    /// Basic foreign key resolver.
    /// </summary>
    /// <todo>Normalize exact compares with FieldPolicy.ForValue(..., FieldKind.Code); avoid double-normalization elsewhere.</todo>
    /// <todo>Honor Strict flag; only fall back to fuzzy/token overlap when Strict=false.</todo>
    /// <todo>Use registry alias resolution when available; still work without registry.</todo>
    /// <todo>Add cancellation tokens if doing any heavy scans in future.</todo>
    internal sealed class ForeignKeyResolver : IForeignKeyResolver
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly ExactFieldMatcher m_matcher = new();

        public ForeignKeyResolver(IVocabularyRegistry registry) => m_registry = registry;

        /// <summary>
        /// Try to resolve a record from a source vocabulary to a target vocabulary by foreign key.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public MatchResult TryResolve(MultiLevelKey record, IControlledVocabulary source, IControlledVocabulary target)
        {
            // Resolve to schema-safe name
            var targetName = FieldPolicy.ForSchema(target.VocabularyName);

            // Explore foreign key defintions in the source vocabulary
            foreach (var sourceFieldName in source.FieldNames)
            {
                var spec = source.GetKeyFieldDescriptor(sourceFieldName)?.ForeignKey ?? null;
                if (spec == null) continue;

                // ForeignKeySpec fields are schema-safe
                var specTargetName = spec.TargetVocabulary;
                var specTargetField = spec.TargetField ?? string.Empty;

                // Accept if alias resolves to 'target' OR the schema names match directly.
                // Otherwise, skip this FK spec.
                if (m_registry.TryGetByNameOrAlias(specTargetName, out var fkTarget))
                {
                    if (!ReferenceEquals(fkTarget, target))
                        continue;
                }
                else
                {
                    // No alias entry: fall back to schema-name equality
                    if (!string.Equals(targetName, specTargetName, StringComparison.Ordinal))
                        continue;
                }

                // Ok, the current sourceFieldName points at 'target'. Skip if we don't have a key value to compare with
                var inputValue = record.GetField(sourceFieldName)?.ToString(false);
                if (string.IsNullOrWhiteSpace(inputValue)) 
                    continue;

                // 1) Try to match foreign key by exploring the target field, if provided
                if (!string.IsNullOrEmpty(specTargetField) && target.FieldNames.Contains(specTargetField))
                {
                    foreach (var r in target.Records)
                    {
                        var a = FieldPolicy.ForValue(r.GetField(specTargetField)!.ToString(false), FieldKind.Code);
                        var b = FieldPolicy.ForValue(inputValue, FieldKind.Code);

                        if (m_matcher.Score(a, b) == 1)
                        {
                            return new MatchResult
                            {
                                SourceVocabulary = source.VocabularyName,
                                SourceField = sourceFieldName,
                                SourceFieldValue = inputValue,
                                TargetVocabulary = target.VocabularyName,
                                TargetField = specTargetField,
                                TargetFieldValue = inputValue,
                                Score = 100,
                                MatchedKey = r,
                                StrategyUsed = MatchStrategy.Exact,
                                Justification = $"FK '{sourceFieldName}':={target.VocabularyName}.{spec.TargetField}"
                            };
                        }
                    }
                }

                // 2) Fallback: exact against any compatible target field
                var inputKey = MultiLevelKey.FromPairs([(sourceFieldName, inputValue!)], target.Domain, strict: false);
                foreach (var fieldname in target.FieldNames)
                {
                    var mapping = new ControlledVocabularies.Resolve.StrategyKeyResolver.FieldMapping(sourceFieldName, fieldname)
                    {
                        Strategy = MatchStrategy.Exact,
                        Weight = 1
                    };
                    var resolver = new ControlledVocabularies.Resolve.StrategyKeyResolver(target.Records, new[] { mapping });
                    var match = resolver.FindBestMatch(inputKey);
                    if (match != null && match.Score > 0)
                    {
                        // Complement fields not filled out byt the resolver
                        match.SourceVocabulary = source.VocabularyName;
                        match.TargetVocabulary = target.VocabularyName;
                        match.Justification = $"FK fallback (Exact) '{sourceFieldName}':={target.VocabularyName}.{fieldname}";
                        return match;
                    }
                }
            }

            return NoMatchNamed(source, target, "No FK rule produced a match.");
        }
        static MatchResult NoMatchNamed(IControlledVocabulary a, IControlledVocabulary b, string why) => new MatchResult
        {
            SourceVocabulary = a.VocabularyName,
            TargetVocabulary = b.VocabularyName,
            Score = 0,
            Justification = why
        };
    }
}