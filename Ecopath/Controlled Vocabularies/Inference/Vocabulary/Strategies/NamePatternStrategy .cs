using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Inference.Vocabulary;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Vocabulary.Strategies
{
    public sealed class NamePatternVocabularyStrategy : IVocabularyInferenceStrategy
    {
        private readonly IVocabularyRegistry? _registry;
        private readonly ContainsFieldMatcher _contains = new ContainsFieldMatcher();

        public NamePatternVocabularyStrategy(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
        }

        public string Name => "NamePattern";
        public double Priority => 9.0;

        public VocabularyStrategyResult Analyze(IControlledVocabulary vocabulary, ModelContext? modelContext, IVocabularyRegistry? registry)
        {
            var result = new VocabularyStrategyResult();
            var name = FieldPolicy.ForSchema(vocabulary.VocabularyName);

            // Registry similarity (name containment in either direction)
            var reg = _registry ?? registry;
            if (reg != null)
            {
                var all = reg.GetAll().GetEnumerator();
                while (all.MoveNext())
                {
                    var other = all.Current;
                    if (object.ReferenceEquals(other, vocabulary)) continue;

                    var otherName = FieldPolicy.ForSchema(other.VocabularyName);
                    var hit1 = _contains.Score(otherName, name) > 0.0;
                    var hit2 = _contains.Score(name, otherName) > 0.0;
                    if (hit1 || hit2)
                    {
                        result.AddDomainHint(other.Domain, 0.8, "Name similar to registered vocabulary");
                        result.AddPurposeHint(other.Purpose, 0.8, "Purpose aligned with similar vocabulary");
                    }
                }
            }

            // Generic tokens
            if (_contains.Score("species", name) > 0.0 ||
                _contains.Score("taxon", name) > 0.0 ||
                _contains.Score("fish", name) > 0.0 ||
                _contains.Score("marine", name) > 0.0 ||
                _contains.Score("biological", name) > 0.0)
            {
                result.AddDomainHint(KeyDomain.Species, 0.7, "Name suggests species domain");
                result.AddPurposeHint(KeyPurpose.Species, 0.6, "Likely species purpose");
            }

            if (_contains.Score("lifestage", name) > 0.0 ||
                _contains.Score("stage", name) > 0.0)
            {
                result.AddDomainHint(KeyDomain.Species, 0.7, "Name suggests species domain");
                result.AddPurposeHint(KeyPurpose.Lifestage, 0.9, "Likely lifestage purpose");
            }

            if (_contains.Score("gear", name) > 0.0 ||
                _contains.Score("fleet", name) > 0.0 ||
                _contains.Score("metier", name) > 0.0 ||
                _contains.Score("vessel", name) > 0.0 ||
                _contains.Score("segment", name) > 0.0)
            {
                result.AddDomainHint(KeyDomain.FleetSegment, 0.8, "Name suggests fleet/metier domain");
                result.AddPurposeHint(KeyPurpose.Gear, 0.6, "Gear-related");
                result.AddPurposeHint(KeyPurpose.Fleet, 0.6, "Fleet-related");
            }

            if (_contains.Score("country", name) > 0.0 ||
                _contains.Score("nation", name) > 0.0)
            {
                result.AddDomainHint(KeyDomain.Country, 0.8, "Name suggests country domain");
                result.AddPurposeHint(KeyPurpose.Country, 0.8, "Country codes/names");
            }

            return result;
        }
    }
}
