using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Match;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Strategies
{
    public sealed class DomainFromNameStrategy : IVocabularyAnalysisStrategy
    {
        private readonly IVocabularyRegistry? _registry;
        private readonly ContainsFieldMatcher _contains = new ContainsFieldMatcher();

        public DomainFromNameStrategy(IVocabularyRegistry? registry = null)
        {
            _registry = registry;
        }

        public string Name => "DomainFromName";
        public double Priority => 9.0;

        public void Analyze(IControlledVocabulary vocab, ModelContext context, SemanticInferenceResult acc, System.Func<string, FieldInferenceInfo> fieldInfoProvider)
        {
            var name = FieldPolicy.ForSchema(vocab.VocabularyName);

            // Leverage existing registry names (similarity by containment)
            if (_registry != null)
            {
                var all = _registry.GetAll();
                foreach (var other in all)
                {
                    var otherName = FieldPolicy.ForSchema(other.VocabularyName);
                    var hit1 = _contains.Score(otherName, name) > 0.0;
                    var hit2 = _contains.Score(name, otherName) > 0.0;
                    if (hit1 || hit2)
                    {
                        acc.AddDomainHint(other.Domain, 0.8, "Name similarity to registered vocabulary");
                        acc.AddPurposeHint(other.Purpose, 0.8, "Purpose inferred via similar vocabulary");
                    }
                }
            }

            // Heuristics by tokens (normalized)
            if (_contains.Score("species", name) > 0.0 ||
                _contains.Score("taxon", name) > 0.0 ||
                _contains.Score("fish", name) > 0.0 ||
                _contains.Score("marine", name) > 0.0 ||
                _contains.Score("biological", name) > 0.0)
            {
                acc.AddDomainHint(KeyDomain.Species, 0.7, "Vocabulary name suggests species domain");
                acc.AddPurposeHint(KeyPurpose.Species, 0.6, "Name aligns with species purpose");
            }

            if (_contains.Score("lifestage", name) > 0.0 || _contains.Score("stage", name) > 0.0)
            {
                acc.AddDomainHint(KeyDomain.Species, 0.7, "Name suggests species domain");
                acc.AddPurposeHint(KeyPurpose.Lifestage, 0.9, "Name suggests life stage purpose");
            }

            // Fleet/Metier signals (per your note)
            if (_contains.Score("gear", name) > 0.0 ||
                _contains.Score("fleet", name) > 0.0 ||
                _contains.Score("metier", name) > 0.0 ||
                _contains.Score("vessel", name) > 0.0 ||
                _contains.Score("segment", name) > 0.0)
            {
                acc.AddDomainHint(KeyDomain.FleetSegment, 0.8, "Name suggests fleet/metier domain");
                acc.AddPurposeHint(KeyPurpose.Gear, 0.6, "Likely gear-related");
                acc.AddPurposeHint(KeyPurpose.Fleet, 0.6, "Likely fleet-related");
            }

            // Geographic / Country signals
            if (_contains.Score("country", name) > 0.0 ||
                _contains.Score("nation", name) > 0.0)
            {
                acc.AddDomainHint(KeyDomain.Country, 0.8, "Name suggests country domain");
                acc.AddPurposeHint(KeyPurpose.Country, 0.8, "Likely country codes/names");
            }
        }
    }
}
