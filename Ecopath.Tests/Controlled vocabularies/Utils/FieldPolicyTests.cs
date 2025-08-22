using ControlledVocabularies.Core;
using ControlledVocabularies.Vocabularies;
using FluentAssertions;
using System.Text;
using Xunit;

namespace ControlledVocabularies.Utils.Tests
{
    public class FieldPolicyTests
    {
        [Fact]
        public void Schema_Normalization_Bridges_Hyphen_To_Space()
        {
            FieldPolicy.ForSchema("Alpha3_Code").Should().Be("alpha3 code");
            FieldPolicy.ForSchema("alpha-3").Should().Be("alpha 3");
            FieldPolicy.ForSchema("  GEAR_CODE ").Should().Be("gear code");
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
            var iso = new ISO3166CountryCodeVocabulary();
            iso.Load().Should().BeTrue();

            iso.FieldNames.Should().Contain("alpha 3"); // schema key form

            // emulate authored FK with "alpha-3"
            var authored = "alpha-3";
            var tf = FieldPolicy.ForSchema(authored);
            tf.Should().Be("alpha 3");
            var any = iso.Records.First();
            any.GetField(tf).Should().NotBeNull();
        }
    }
}