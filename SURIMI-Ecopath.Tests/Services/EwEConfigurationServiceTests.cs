using Ecopath.EwE;
using Ecopath.Services;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Registries;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ecopath.Tests.Services;

public class EwEConfigurationServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateConfigurationAsync_RequiresExactModelKey(bool modelExists)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        const string modelKey = "scenario/scenario.eiixml";
        var root = Path.Combine(Path.GetTempPath(), "ecopath-test-inputs");
        var modelPath = Path.Combine(root, "scenario", "scenario.eiixml");
        var store = new Mock<IBlobStore>(MockBehavior.Strict);
        store.Setup(s => s.ExistsAsync(modelKey, PathType.Input, token)).ReturnsAsync(modelExists);
        if (modelExists)
        {
            store.SetupGet(s => s.LocalInputRoot).Returns(root);
            store.Setup(s => s.CopyToLocalDirectoryOrIgnoreAsync("scenario", PathType.Input, token))
                .ReturnsAsync(new[] { modelPath, Path.Combine(root, "scenario", "another-export.eiixml") });
        }
        var service = new EwEConfigurationService(Mock.Of<IKeyFieldDescriptorRegistry>(), new MultiLevelKeyFactory(),
            NullLogger<EwEConfiguration>.Instance, Mock.Of<IVocabularyRegistry>(), store.Object, Mock.Of<ISurimiContractToEwEService>());

        // Act
        var result = service.CreateConfigurationAsync("scenario", token);

        // Assert
        if (modelExists)
        {
            var configuration = await result;
            Assert.Equal(modelPath, configuration.LocalModelFile);
            Assert.Equal("scenario.eiixml", configuration.ModelName);
        }
        else
        {
            var error = await Assert.ThrowsAsync<FileNotFoundException>(() => result);
            Assert.Equal(modelKey, error.FileName);
        }
        store.Verify(s => s.ExistsAsync(modelKey, PathType.Input, token), Times.Once);
        store.Verify(s => s.CopyToLocalDirectoryOrIgnoreAsync("scenario", PathType.Input, token), modelExists ? Times.Once() : Times.Never());
    }
}
