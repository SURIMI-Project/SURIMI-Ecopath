using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.ForeignKeys;
using Eii.ControlledVocabularies.Inference.Field;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies.Country;
using FluentAssertions;
using Microsoft.Extensions.FileSystemGlobbing;
using System.Text;
using Xunit;

namespace ControlledVocabularies.Utils.Tests
{
    public class FieldPolicyTests
    {
        private IVocabularyRegistry m_vocabularyRegistry;
        private IKeyFieldDescriptorIndexer _keyFieldDescriptorIndexer;
        private GenericVocabularyMatcher m_matcher;                 // TODO: Why not IVocabularyMatcher??
        private readonly IFieldInferenceOrchestrator m_fieldInferenceOrchestrator;

        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry; public FieldPolicyTests()
        {
            // fresh registry per test class
            m_vocabularyRegistry = new VocabularyRegistry();
            m_keyFieldDescriptorRegistry = new KeyFieldDescriptorRegistry();
            _keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();
            var foreignKeyResolver = new ForeignKeyResolver(m_vocabularyRegistry, m_keyFieldDescriptorRegistry);
            var vocabularyMatcher = new GenericVocabularyMatcher(m_vocabularyRegistry, foreignKeyResolver, m_keyFieldDescriptorRegistry);
            m_matcher = new GenericVocabularyMatcher(m_vocabularyRegistry, foreignKeyResolver, m_keyFieldDescriptorRegistry);
            m_fieldInferenceOrchestrator = new FieldInferenceOrchestrator(m_vocabularyRegistry, m_keyFieldDescriptorRegistry, vocabularyMatcher);
        }

        [Fact]
        public void Schema_Normalization_Bridges_Hyphen_To_Space()
        {
            FieldPolicy.ForSchema("Alpha3_Code").Should().Be("alpha3-code");
            FieldPolicy.ForSchema("alpha 3").Should().Be("alpha-3");
            FieldPolicy.ForSchema("  GEAR_CODE ").Should().Be("gear-code");
        }

        [Fact]
        public void Value_Normalization_Respects_Kind()
        {
            FieldPolicy.ForValue(" esp ", FieldKind.Code).Should().Be("ESP");
            FieldPolicy.ForValue("Gadus morhua", FieldKind.Label).Should().Be("gadus morhua");
            FieldPolicy.ForValue("Drifting longlines!", FieldKind.Label).Should().Be("drifting longlines");
            var uri = "https://vocab.nerc.ac.uk/collection/S11/current/ABC";
            FieldPolicy.ForValue(uri, FieldKind.Uri).Should().Be(uri.Normalize(NormalizationForm.FormC));
        }

        [Fact]
        public void Iso3166_FieldName_Is_Schema_Key_And_FK_With_Hyphen_Works()
        {
            var iso = new ISO3166CountryCodeVocabulary(m_fieldInferenceOrchestrator);
            var vocabularyRegistry = new VocabularyRegistry();
            var keyFieldDescriptorIndexer = new KeyFieldDescriptorIndexer();

            iso.Load(keyFieldDescriptorIndexer).Should().BeTrue();

            iso.FieldNames.Should().Contain("alpha-3"); // schema key form

            // emulate authored FK with "ALPHA_3"
            var authored = "ALPHA 3";
            var tf = FieldPolicy.ForSchema(authored);
            tf.Should().Be("alpha-3");
            var any = iso.Records.First();
            any.GetField(tf).Should().NotBeNull();
        }
    }
}