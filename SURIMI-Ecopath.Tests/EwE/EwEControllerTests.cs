using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SURIMI.Datamodel;
using Xunit;

namespace Ecopath.Tests.EwE
{
    public class EwEControllerTests
    {
        private readonly Mock<ILogger<EwEController>> _loggerMock = new();
        private readonly Mock<IEwEConfigurationService> _configServiceMock = new();
        private readonly Mock<IEwECore> _coreMock = new();
        private readonly Mock<IPluginManager> _pluginManagerMock = new();
        private readonly Mock<IKeyFieldDescriptorRegistry> _keyFieldDescriptorRegistryMock = new Mock<IKeyFieldDescriptorRegistry>();
        private readonly Mock<IMultiLevelKeyFactory> _multiLevelKeyFactoryMock = new Mock<IMultiLevelKeyFactory>();
        private readonly Mock<IBlobStore> _blobStoreMock = new();
        private readonly Mock<IVocabulariesRegisterService> _vocabulariesRegisterServiceMock = new Mock<IVocabulariesRegisterService>();

        public EwEControllerTests()
        {
            // No plugin/bridge setup here — Initialize() is now called in StartAsync, not in the constructor.
            // Tests that exercise StartAsync should set up _coreMock.Initialize(), PluginManager, LoadPlugins(), and GetPlugins().
        }

        [Fact]
        public void Constructor_SetsRunStateToIdle()
        {
            // Arrange


            // Act
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);

            // Assert
            controller.RunState.Should().Be(EwEController.RunStates.idle);
            // Initialize() must NOT be called at construction — cCore is created lazily in StartAsync
            _coreMock.Verify(c => c.Initialize(), Times.Never);
        }

        [Fact]
        public void StopAsync_TeardownsCore_AndReturnsTrue()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);

            // Act
            var result = controller.StopAsync().Result;

            // Assert
            result.Should().BeTrue();
            _coreMock.Verify(c => c.Teardown(), Times.Once);
        }

        [Fact]
        public void IsWaiting_ReturnsTrue_WhenRunStateIsWaiting()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);
            typeof(EwEController)
                .GetProperty("RunState")!
                .SetValue(controller, EwEController.RunStates.waiting);

            // Act & Assert
            controller.IsWaiting.Should().BeTrue();
        }

        [Fact]
        public async Task UpdatePricesAsync_SetsPricesIn_ReturnsTrue()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);
            var prices = new List<SpeciesPrice>
        {
            new SpeciesPrice
            {
                SpeciesCode = "SPC",
                MarketCode = "MKT",
                CategoryCode = "",
                Price = 10,
                Currency = "EUR",
                Timestamp = DateTime.UtcNow
            }
        };

            // Act
            var result = await controller.UpdatePricesAsync(prices);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateCatchDispositionSummaryAsync_SetsCatchIn_ReturnsTrue()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);
            var summary = new CatchDispositionSummary();

            // Act
            var result = await controller.UpdateCatchDispositionSummaryAsync(summary);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GetBiomassAsync_ReturnsBiomass()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);

            // Act
            var biomass = await controller.GetBiomassAsync();

            // Assert
            biomass.Should().NotBeNull();
            biomass.MeasurementUnit.Should().Be("kg"); // Assuming the default unit is kg
        }

        [Fact]
        public async Task GetSalesSummariesAsync_ReturnsSalesSummaries()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);

            // Act
            var sales = await controller.GetSalesSummariesAsync(DateTime.UtcNow, DateTime.UtcNow);

            // Assert
            sales.Should().NotBeNull();
        }

        [Fact]
        public async Task GetCatchDispositionSummaryAsync_ReturnsCatchDispositionSummary()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object);

            // Act
            var summary = await controller.GetCatchDispositionSummaryAsync(DateTime.UtcNow, DateTime.UtcNow);

            // Assert
            summary.Should().NotBeNull();
        }
    }
}
