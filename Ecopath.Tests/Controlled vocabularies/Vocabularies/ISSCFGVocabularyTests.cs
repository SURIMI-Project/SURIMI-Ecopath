using ControlledVocabularies.Core;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Vocabularies.Tests
{
    public class ISSCFGVocabularyTests
    {
        [Fact]
        public void ISSCFG_Loads_And_Exposes_Core_Fields()
        {
            var v = new ISSCFGGearCodeVocabulary();
            v.Load().Should().BeTrue();

            v.VocabularyName.Should().Be("ISSCFG");
            v.Domain.Should().Be(KeyDomain.FleetSegment);
            (v.Purpose & (KeyPurpose.Fleet | KeyPurpose.Gear)).Should().NotBe(0);
            v.CodeFieldName.Should().Be("GEAR_CODE".ToLowerInvariant()); // base normalizes names

            // Must have at least some rows
            v.Records.Should().NotBeEmpty();
        }

        [Fact]
        public void ISSCFG_FindCode_Fuzzy_On_Gear_Name()
        {
            var v = new ISSCFGGearCodeVocabulary();
            v.Load().Should().BeTrue();

            // "Drifting longlines" → "DL"
            var code = v.FindCode("Drifting longlines");
            code.Should().Be("DL");
        }
    }
}