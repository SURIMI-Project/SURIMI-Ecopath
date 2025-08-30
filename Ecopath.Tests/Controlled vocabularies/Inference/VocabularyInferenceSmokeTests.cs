using ControlledVocabularies.Core;
using ControlledVocabularies.Inference.Vocabulary;
using ControlledVocabularies.Inference.Vocabulary.Strategies;
using ControlledVocabularies.Registries;
using ControlledVocabularies.Utils;
using ControlledVocabularies.Vocabularies;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Inference.Tests
{
    public class VocabularyInferenceSmokeTests
    {
        private readonly VocabularyRegistry _registry;
        private readonly VocabularyInferenceOrchestrator _orchestrator;

        public VocabularyInferenceSmokeTests()
        {
            _registry = new VocabularyRegistry();

            // Register known vocabs
            var vocabs = new IControlledVocabulary[]
            {
                new ASFISSpeciesCodeVocabulary(),
                new WoRMSSpeciesVocabulary(),
                new SURIMILifestageVocabulary(),
                new NERCLifeStageVocabulary(),
                new ISSCFGGearCodeVocabulary(),
                new ISO3166CountryCodeVocabulary()
            };

            var i = 0;
            while (i < vocabs.Length)
            {
                _registry.Register(vocabs[i]);
                i++;
            }

            // Build inference orchestrator with strategies
            _orchestrator = new VocabularyInferenceOrchestrator();
            _orchestrator.RegisterStrategy(new NamePatternVocabularyStrategy(_registry));
            _orchestrator.RegisterStrategy(new ForeignKeyDiscoveryStrategy(_registry));
        }

        [Theory]
        [InlineData(typeof(ASFISSpeciesCodeVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(WoRMSSpeciesVocabulary), KeyDomain.Species, KeyPurpose.Species)]
        [InlineData(typeof(SURIMILifestageVocabulary), KeyDomain.Species, KeyPurpose.Lifestage)]
        [InlineData(typeof(NERCLifeStageVocabulary), KeyDomain.Species, KeyPurpose.Lifestage)]
        [InlineData(typeof(ISSCFGGearCodeVocabulary), KeyDomain.FleetSegment, KeyPurpose.Gear | KeyPurpose.Fleet)]
        [InlineData(typeof(ISO3166CountryCodeVocabulary), KeyDomain.Country, KeyPurpose.Country)]
        public void Should_Infer_Primary_Domain_And_Purpose(Type vocabType, KeyDomain expectedDomain, KeyPurpose expectedPurpose)
        {
            var vocab = (IControlledVocabulary)Activator.CreateInstance(vocabType)!;
            vocab.Load();

            var result = _orchestrator.Analyze(vocab, null, registry: _registry);

            result.InferredDomain.Should().Be(expectedDomain);
            result.InferredPurpose.Should().Be(expectedPurpose);
            result.DomainConfidence.Should().BeGreaterThan(0.3); // loose smoke threshold
        }

        [Fact]
        public void Should_Find_WoRMS_to_ASFIS_FK_Candidate()
        {
            var worms = new WoRMSSpeciesVocabulary();
            var asfis = new ASFISSpeciesCodeVocabulary();
            worms.Load();
            asfis.Load();

            // Only want to explore asfis here
            _registry.Clear();
            //_registry.Register(worms);
            _registry.Register(asfis);

            var res = _orchestrator.Analyze(worms, null, registry: _registry);

            bool found = false;
            foreach (var fk in res.ForeignKeyCandidates) 
            {
                if (fk == null) continue;

                // We only normalize the target vocabulary name for robust comparison
                var targetName = fk.TargetVocabulary ?? "";
                var targetSchema = FieldPolicy.ForSchema(targetName);

                if (string.Equals(targetSchema, "asfis", StringComparison.Ordinal))
                {
                    found = true;
                    fk.Score.Should().BeGreaterThan(10);
                    fk.Confidence.Should().BeGreaterThan(0.2);
                    break;
                }
            }

            found.Should().BeTrue("Expected an FK candidate from WoRMS FAO_Code to ASFIS Alpha3_Code");
        }
    }
}
