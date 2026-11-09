using System.Reflection;
using Ecopath.EwE;
using Ecopath.EwE.Wrapper;
using Ecopath.Services;
using Eii.BlobStore;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using EwEBridge.Ecospace;
using EwECore;
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
        private readonly Mock<IHcrInitialisationFileService> _hcrInitialisationFileServiceMock = new();
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);

            // Assert
            controller.RunState.Should().Be(EwEController.RunStates.idle);
            // Initialize() must NOT be called at construction — cCore is created lazily in StartAsync
            _coreMock.Verify(c => c.Initialize(), Times.Never);
        }

        [Theory]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        public void EndTimeStep_CapturesHcrCatchOnlyDuringEnabledSpinUp(bool enabled, bool inSpinUp, bool shouldPause)
        {
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);
            var spaceData = new cEcospaceDataStructures(null!) { bInSpinUp = inSpinUp };
            _coreMock.SetupGet(core => core.EcospaceDataStructures).Returns(spaceData);
            _coreMock.Setup(core => core.EcospaceTimestepToAbsoluteTime(It.IsAny<int>()))
                .Returns(new DateTime(shouldPause ? 2013 : 2012, 1, 1));
            _configServiceMock.Setup(service => service.Mappings(KeyDomain.Species)).Returns(new List<EwEMapping>());
            _hcrInitialisationFileServiceMock.SetupGet(service => service.Enabled).Returns(enabled);
            typeof(EwEController).GetField("_configuration", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(controller, new EwEConfiguration { StartYear = 2013 });

            typeof(EwEController).GetMethod("BridgeCallback", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(controller, new object[] { cEcospaceBridgePlugin.EventType.EndTimeStep, 1 });

            _hcrInitialisationFileServiceMock.Verify(service => service.CaptureCatchTimestep(It.IsAny<CatchDispositionSummary>()),
                enabled && inSpinUp ? Times.Once() : Times.Never());
            _coreMock.VerifySet(core => core.EcospacePaused = true, shouldPause ? Times.Once() : Times.Never());
            _coreMock.VerifyGet(core => core.EcopathDataStructures,
                shouldPause || (enabled && inSpinUp) ? Times.Once() : Times.Never());
        }

        [Fact]
        public async Task StopAsync_TeardownsCore_AndReturnsTrue()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);

            // Act
            var result = await controller.StopAsync();

            // Assert
            result.Should().BeTrue();
            _coreMock.Verify(c => c.Teardown(), Times.Once);
        }

        [Fact]
        public void IsWaiting_ReturnsTrue_WhenRunStateIsWaiting()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);
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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);

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
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);

            // Act
            var sales = await controller.GetSalesSummariesAsync(DateTime.UtcNow, DateTime.UtcNow);

            // Assert
            sales.Should().NotBeNull();
        }

        [Fact]
        public async Task GetCatchDispositionSummaryAsync_ReturnsCatchDispositionSummary()
        {
            // Arrange
            var controller = new EwEController(_loggerMock.Object, _configServiceMock.Object, _coreMock.Object, _keyFieldDescriptorRegistryMock.Object, _multiLevelKeyFactoryMock.Object, _vocabulariesRegisterServiceMock.Object, _blobStoreMock.Object, _hcrInitialisationFileServiceMock.Object);

            // Act
            var summary = await controller.GetCatchDispositionSummaryAsync(DateTime.UtcNow, DateTime.UtcNow);

            // Assert
            summary.Should().NotBeNull();
        }
    }
}
