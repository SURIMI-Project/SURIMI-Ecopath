using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies;
using FluentAssertions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Win32;
using Xunit;

namespace ControlledVocabularies.Descriptors.Tests
{
    public class KeyFieldIndexerTests
    {
        private readonly IVocabularyRegistry m_registry;
        private readonly IKeyFieldDescriptorIndexer m_keyFieldDescriptorIndexer;
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly IVocabularyMatcher m_vocabularyMatcher;
        private readonly ForeignKeyResolver m_fkResolver;

        public KeyFieldIndexerTests()
        {
            m_registry = new VocabularyRegistry();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            m_keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            m_fkResolver = new ForeignKeyResolver(m_registry, m_keyFieldDescriptorRegistry);
            m_vocabularyMatcher = new GenericVocabularyMatcher(m_registry, m_fkResolver, m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_registry, m_keyFieldDescriptorRegistry, m_vocabularyMatcher);
        }

        [Fact]
        public void Indexer_Infers_Kind_And_Strategy_For_Code()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species, m_fieldInferenceOrchestrator);
            v.AddField(SpeciesFields.SpeciesCode, KeyPurpose.Species, FieldKind.Code, strategy: MatchStrategy.None);
            v.AddRow((SpeciesFields.SpeciesCode, "HKE"));
            v.AddRow((SpeciesFields.SpeciesCode, "GUP"));
            v.AddRow((SpeciesFields.SpeciesCode, "MUL"));
            v.Load(m_keyFieldDescriptorIndexer).Should().BeTrue();

            var d = v.GetKeyFieldDescriptor(SpeciesFields.SpeciesCode)!;
            d.Kind.Should().Be(FieldKind.Code);
            d.Strategy.HasFlag(MatchStrategy.Exact).Should().BeTrue();
        }

        [Fact]
        public void Indexer_Infers_Label_With_Fuzzy_And_TokenOverlap()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species, m_fieldInferenceOrchestrator);
            v.AddField("name", KeyPurpose.NotSet, FieldKind.Unknown, strategy: MatchStrategy.None);

            foreach (var s in Enumerable.Range(0, 150))
                v.AddRow(("name", $"Drifting longlines variant {s}"));

            v.Load(m_keyFieldDescriptorIndexer).Should().BeTrue();

            var d = v.GetKeyFieldDescriptor("name")!;
            d.Kind.Should().Be(FieldKind.Label);
            d.Strategy.HasFlag(MatchStrategy.Keyword).Should().BeTrue(); // Too long for Fuzzy
            d.Strategy.HasFlag(MatchStrategy.TokenOverlap).Should().BeTrue();
        }

        [Fact]
        public void Indexer_Infers_Uri_As_NoMatchable()
        {
            var v = new InMemoryVocabulary("T", KeyDomain.Species, KeyPurpose.Species, m_fieldInferenceOrchestrator);
            v.AddField("link", KeyPurpose.Species, FieldKind.Uri, strategy:MatchStrategy.None);
            v.AddRow(("link", "https://example.org/A"));
            v.AddRow(("link", "https://example.org/B"));

            v.Load(m_keyFieldDescriptorIndexer).Should().BeTrue();

            var d = v.GetKeyFieldDescriptor("link")!;
            d.Kind.Should().Be(FieldKind.Uri);
            d.Strategy.Should().Be(MatchStrategy.Exact);
        }
    }
}

