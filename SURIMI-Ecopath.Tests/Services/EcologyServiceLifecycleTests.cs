using Ecopath.EwE;
using Ecopath.Services;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Surimi;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI.Common.gRPC.Services;
using Xunit;
using EcologyService = Ecopath.Services.EcologyService;

namespace Ecopath.Tests.Services;

public class EcologyServiceLifecycleTests
{
    private readonly Mock<IEwEController> m_controller = new();
    private readonly Mock<ISimulationScopeManager> m_scopeManager = new();

    public EcologyServiceLifecycleTests()
    {
        m_scopeManager.Setup(manager => manager.CreateSimulationScope(It.IsAny<string>())).Returns(m_controller.Object);
        m_scopeManager.Setup(manager => manager.GetSimulation(It.IsAny<string>())).Returns(m_controller.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitialiseSimulation_RemovesScopeOnlyWhenInitialisationFails(bool succeeds)
    {
        // Arrange
        var service = CreateService();
        var request = JsonParser.Default.Parse<InitialiseSimulationRequest>("""
            {
                "simulationId": "simulation",
                "scenarioName": "scenario",
                "simulation": {
                    "startDateTime": "2013-01-01T00:00:00Z",
                    "maximumEndDateTime": "2050-01-01T00:00:00Z",
                    "geography": { "rasterCellOrigin": 1, "crs": {} },
                    "standards": { "measurements": {} },
                    "items": {}
                }
            }
            """);
        m_controller.Setup(controller => controller.StartAsync(It.IsAny<SURIMI.Datamodel.SurimiContract>(), "scenario", "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(succeeds ? 1 : -1);

        // Act
        var action = () => service.InitialiseSimulation(request, Mock.Of<ServerCallContext>());
        if (succeeds)
            await action();
        else
            await action.Should().ThrowAsync<RpcException>();

        // Assert
        m_scopeManager.Verify(manager => manager.CreateSimulationScope("simulation"), Times.Once());
        m_scopeManager.Verify(manager => manager.RemoveSimulationScope("simulation"), succeeds ? Times.Never() : Times.Once());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EndSimulation_StopsControllerBeforeRemovingScope(bool cancel, bool fails)
    {
        // Arrange
        var service = CreateService();
        m_controller.Setup(controller => controller.StopAsync(10000))
            .Callback(() => m_scopeManager.Verify(manager => manager.RemoveSimulationScope("simulation"), Times.Never()))
            .ReturnsAsync(!fails);

        // Act
        Func<Task> action = async () =>
        {
            if (cancel)
                await service.CancelSimulation(new CancelSimulationRequest { SimulationId = "simulation" }, Mock.Of<ServerCallContext>());
            else
                await service.FinaliseSimulation(new FinaliseSimulationRequest { SimulationId = "simulation" }, Mock.Of<ServerCallContext>());
        };
        if (fails)
            await action.Should().ThrowAsync<RpcException>();
        else
            await action();

        // Assert
        m_controller.Verify(controller => controller.StopAsync(10000), Times.Once());
        m_scopeManager.Verify(manager => manager.RemoveSimulationScope("simulation"), Times.Once());
    }

    [Fact]
    public async Task SimulateStep_ContinuesTheControllerOfTheSimulation()
    {
        // Arrange
        m_controller.Setup(controller => controller.ContinueAsync(60000)).ReturnsAsync(true);
        var service = CreateService();

        // Act
        var response = await service.SimulateStep(new SimulateStepRequest { SimulationId = "simulation" }, Mock.Of<ServerCallContext>());

        // Assert
        response.SimulationId.Should().Be("simulation");
        m_scopeManager.Verify(manager => manager.GetSimulation("simulation"), Times.Once());
        m_controller.Verify(controller => controller.ContinueAsync(60000), Times.Once());
    }

    private EcologyService CreateService()
    {
        return new EcologyService(NullLogger<EcologyService>.Instance, m_scopeManager.Object,
            new ProtocolVersionService(), Mock.Of<IInitialStateFileService>());
    }
}
