using ControlledVocabularies.Core;
using ControlledVocabularies.ForeignKeys;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Resolve;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Match
{
    /// <summary>
    /// 
    /// </summary>
    /// <todo>Overloads already added; ensure registry is optional DI (falls back to GlobalServiceLocator).</todo>
    /// <todo>Add MatchAll(.., topN) to return N best targets and include source/target vocab names in results.</todo>
    /// <todo>Deterministic tie-breaks (current: score desc, then key asc) — document and test.</todo>
    /// <todo>Emit trace in DEBUG for strategy selection and FK fast-path hits.</todo>
    public class GenericVocabularyMatcher : IVocabularyMatcher
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IForeignKeyResolver m_fk;

        public GenericVocabularyMatcher(IVocabularyRegistry? registry = null, IForeignKeyResolver? fkResolver = null)
        {
            m_registry = registry
                ?? Ecopath.Services.GlobalServiceLocator.Get<VocabularyRegistry>()
                ?? throw new InvalidOperationException("No VocabularyRegistry available.");

            m_fk = fkResolver ?? new ForeignKeyResolver(m_registry);
        }

        /// <summary>
        /// Match a record from a source vocabulary to a target library.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="vocabA"></param>
        /// <param name="vocabB">The target library to search. If null, all 
        /// compatible libraries are explored</param>
        /// <param name="minscore"></param>
        /// <returns></returns>
        public MatchResult Match(MultiLevelKey record, IControlledVocabulary vocabA, IControlledVocabulary? vocabB = null, int? minscore = null)
        {
            var threshold = minscore ?? LocalSettings.DefaultMinScore;
            MatchResult best = MatchResult.NoMatch;

            var targets = (vocabB != null) ? [ vocabB ] : m_registry.GetCompatibleVocabularies(vocabA);

            foreach (var t in targets)
            {
                var mr = MatchAgainst(record, vocabA, t, threshold);
                if (mr.Score > best.Score)
                    best = mr;
            }
            return best;
        }

        /// <summary>
        /// Match a record from a source vocabulary to a targer or any available vocabulary.
        /// </summary>
        /// <param name="record"></param>
        /// <param name="vocabA"></param>
        /// <param name="vocabB"></param>
        /// <param name="minscore"></param>
        /// <returns></returns>
        public MatchResult Match(MultiLevelKey record, string vocabAName, string? vocabBName = null, int? minscore = null)
        {
            if (!m_registry.TryGetByNameOrAlias(vocabAName, out var a) || a is null)
                return MatchResult.NoMatch;

            IControlledVocabulary? b = null;
            if (!string.IsNullOrWhiteSpace(vocabBName))
            {
                if (!m_registry.TryGetByNameOrAlias(vocabBName!, out b))
                    return MatchResult.NoMatch;
            }

            return Match(record, a!, b, minscore);
        }

        #region Internals

        private MatchResult MatchAgainst(MultiLevelKey record, IControlledVocabulary vocabA, IControlledVocabulary vocabB, int threshold)
        {
            if (!MatchHelpers.CanMatch(vocabA, vocabB))
                return NoMatchNamed(vocabA, vocabB, "Incompatible vocabularies (domain/purpose).");

            // 1) FK fast-path (delegated)
            if (m_fk != null)
            {
                var fk = m_fk.TryResolve(record, vocabA, vocabB);
                if (fk.Score == 100)
                    return fk; // already contains names
            }

            // 2) Strategy search across compatible fields/strategies
            MatchResult best = MatchResult.NoMatch;

            foreach (string sourceField in record.FieldNames)
            {
                var sourceValue = record.GetField(sourceField)?.ToString(false);
                if (string.IsNullOrWhiteSpace(sourceValue)) continue;

                var inputKey = MultiLevelKey.FromPairs([(sourceField, sourceValue)], vocabB.Domain, strict: false);

                foreach (var map in BuildMappingsForField(sourceField, vocabB))
                {
                    var resolver = new StrategyKeyResolver(vocabB.Records, new[] { map });
                    var match = resolver.FindBestMatch(inputKey, threshold);

                    if (match != null && match.Score > Math.Max(best.Score, threshold))
                    {
                        match.SourceVocabulary = vocabA.VocabularyName;
                        match.TargetVocabulary = vocabB.VocabularyName;
                        match.Justification = $"Matched '{sourceField}' := '{map.TargetField}' via {map.Strategy}";
                        best = match;
                    }
                }
            }

            return best;
        }

        private static IEnumerable<MatchStrategy> EnumerateFlags(MatchStrategy flags)
        {
            foreach (MatchStrategy f in Enum.GetValues(typeof(MatchStrategy)))
                if (f != MatchStrategy.None && flags.HasFlag(f))
                    yield return f;
        }

        private IEnumerable<FieldMapping> BuildMappingsForField(string sourceField, IControlledVocabulary vocabB, MatchStrategy? sourceHint = null)
        {
            foreach (string targetField in vocabB.FieldNames)
            {
                var descr = vocabB.GetKeyFieldDescriptor(targetField);
                if (descr == null) continue;

                var effective = sourceHint.HasValue ? (descr.Strategy & sourceHint.Value) : descr.Strategy;
                foreach (var strategy in EnumerateFlags(effective))
                {
                    yield return new FieldMapping(sourceField, targetField)
                    {
                        Strategy = strategy,
                        Weight = Math.Max(1, descr.Weight),
                        IsRequired = descr.IsRequired,
                        Kind = descr.Kind == FieldKind.Unknown ? FieldKind.Label : descr.Kind,
                        CaseSensitive = descr.CaseSensitive
                    };
                }
            }
        }

        private static MatchResult NoMatchNamed(IControlledVocabulary a, IControlledVocabulary b, string reason)
            => new MatchResult() 
                    {
                        SourceVocabulary = a.VocabularyName,
                        TargetVocabulary = b.VocabularyName,
                        Score = 0,
                        Justification = reason
                    };

        #endregion // Internals
    }
}
