// ControlledVocabularies.Inference.Vocabulary.Strategies/DescriptorConsensusStrategy.cs
using ControlledVocabularies.Context;
using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Vocabularies;

namespace ControlledVocabularies.Inference.Vocabulary.Strategies
{
    public sealed class DescriptorConsensusStrategy : IVocabularyInferenceStrategy
    {
        public string Name { get { return "DescriptorConsensus"; } }
        public double Priority { get { return 6.0; } }

        public VocabularyStrategyResult Analyze(IControlledVocabulary vocabulary, ModelContext? context, IVocabularyRegistry? registry)
        {
            var result = new VocabularyStrategyResult();

            // scan descriptors quickly; infer coarse domain/purpose cues
            var fields = vocabulary.FieldNames;
            foreach (var fn in fields)
            {
                var d = vocabulary.GetKeyFieldDescriptor(fn);
                if (d == null) continue;

                // Label + Fuzzy often signals names (species, common names)
                if (d.Kind == FieldKind.Label && HasFlag(d.Strategy, MatchStrategy.Fuzzy))
                {
                    result.AddPurposeHint(KeyPurpose.Species, 0.3, "Label+Fuzzy field present.");
                }

                // Exact Code fields are identifiers
                if (d.Kind == FieldKind.Code && d.Strategy == MatchStrategy.Exact)
                {
                    // nudge FK/identifier-heavy datasets toward Gear, Species, Country depending on common tags in field name
                    var ln = fn.ToLowerInvariant();
                    if (IndexOf(ln, "fao") >= 0 || IndexOf(ln, "aphia") >= 0 || IndexOf(ln, "alpha3") >= 0)
                    {
                        result.AddPurposeHint(KeyPurpose.Species, 0.4, "Presence of well-known species codes.");
                    }
                    if (IndexOf(ln, "gear") >= 0 || IndexOf(ln, "metier") >= 0)
                    {
                        result.AddDomainHint(KeyDomain.FleetSegment, 0.4, "Gear/metier code field.");
                        result.AddPurposeHint(KeyPurpose.Gear, 0.4, "Gear code field.");
                    }
                    if (IndexOf(ln, "iso3") >= 0 || IndexOf(ln, "country") >= 0 || IndexOf(ln, "flag") >= 0)
                    {
                        result.AddDomainHint(KeyDomain.Country, 0.4, "Country code field.");
                        result.AddPurposeHint(KeyPurpose.Country, 0.4, "Country code field.");
                    }
                }
            }

            return result;
        }

        private static bool HasFlag(MatchStrategy s, MatchStrategy f) { return (s & f) == f; }
        private static int IndexOf(string s, string sub) { return s.IndexOf(sub, System.StringComparison.Ordinal); }
    }
}
