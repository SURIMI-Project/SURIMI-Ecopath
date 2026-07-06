using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using SURIMI.Datamodel;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using EwECore;
using EwECore.Plugins;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Eii.BlobStore;
using Ecopath.Services;
using Eii.SemanticRegistry;

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
        private readonly Mock<ISemanticRegistry> _semanticRegistryMock = new();
        private readonly Mock<IBlobStore> _blobStoreMock = new();
        private readonly Mock<IVocabulariesRegisterService> _vocabulariesRegisterServiceMock = new Mock<IVocabulariesRegisterService>();

        public EwEControllerTests()
        {
            // Setup default behavior for mocks if necessary
            _coreMock.SetupGet(c => c.PluginManager).Returns(_pluginManagerMock.Object);
            _pluginManagerMock.Setup(pm => pm.LoadPlugins()).Returns(1); // or whatever value you expect
            _pluginManagerMock.Setup(pm => pm.GetPlugins(It.IsAny<Type>(), It.IsAny<cPluginAssembly>())).Returns(new List<IPlugin>());
        }

        [Fact]
        public void Constructor_SetsRunStateToIdle()
        {
            // Arrange


            // Act
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);

            // Assert
            controller.RunState.Should().Be(EwEController.RunStates.idle);
        }

        [Fact]
        public void IsWaiting_ReturnsTrue_WhenRunStateIsWaiting()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);

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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);

            // Act
            var sales = await controller.GetSalesSummariesAsync(DateTime.UtcNow, DateTime.UtcNow);

            // Assert
            sales.Should().NotBeNull();
        }

        [Fact]
        public async Task GetCatchDispositionSummaryAsync_ReturnsCatchDispositionSummary()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object);

            // Act
            var summary = await controller.GetCatchDispositionSummaryAsync(DateTime.UtcNow, DateTime.UtcNow);

            // Assert
            summary.Should().NotBeNull();
        }
    }
}
