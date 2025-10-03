using Eii.ControlledVocabularies.Utils;
using FluentAssertions;
using Xunit;

namespace ControlledVocabularies.Utils.Tests
{
    public class FieldNameHelperTests
    {
        [Fact]
        public void Test_Basic_Behaviour()
        {
            string stem = "";
            FieldNameHelper.IsCodeField("FAO_code", out stem).Should().BeTrue();
            stem.Should().Be("fao");

            FieldNameHelper.IsNameField("Scientific-Name", out stem).Should().BeTrue();
            stem.Should().Be("scientific");

            FieldNameHelper.TryFindPairedField(new[] { "fao-code", "fao-name" }, "fao-code", out var paired, out var rel).Should().BeTrue();
            paired.Should().Be("fao-name");
            rel.Should().Be("CodeName");
        }
    }
}