using ControlledVocabularies.Core;
using ControlledVocabularies.Descriptors;
using ControlledVocabularies.Utils;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
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
            v.CodeFieldName.Should().Be("GEAR_CODE"); // base normalizes names

            // Must have at least some rows
            v.Records.Should().NotBeEmpty();
        }

        [Fact]
        public void ISSCFG_FindCode_Fuzzy_On_Gear_Name()
        {
            var v = new ISSCFGGearCodeVocabulary();
            v.Load().Should().BeTrue();

            // "Drifting longlines" := "DL"
            var code = v.FindCode("Drifting longlines");
            code.Should().Be("DL");
        }

        [Fact]
        public void Should_Correctly_Analyze_ISSCFG_Gear_Code_Field()
        {
            // Arrange
            var isscfgVocab = new ISSCFGGearCodeVocabulary();
            isscfgVocab.Load();

            var indexer = new KeyFieldIndexer();
            var descriptor = new KeyFieldDescriptor("GEAR_CODE", KeyDomain.FleetSegment, KeyPurpose.Gear);

            // Act
            var success = indexer.BuildIndex("GEAR_CODE", isscfgVocab.Records, descriptor);

            // Assert  
            success.Should().BeTrue();
            descriptor.Kind.Should().Be(FieldKind.Code); // Should detect as code due to CODE/NAME pair
            descriptor.Strategy.Should().HaveFlag(MatchStrategy.Exact); // Should use exact matching
            descriptor.Weight.Should().BeGreaterThan(5); // Should get good weight from analysis
        }
    }
}