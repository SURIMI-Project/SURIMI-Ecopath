using Ecopath.EwE;
using Ecopath.Services;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ecopath.Tests.Services;

public class SimulationScopeManagerTests : IDisposable
{
    private readonly List<Mock<IDisposable>> m_disposableControllers = new();
    private readonly ServiceProvider m_serviceProvider;
    private readonly SimulationScopeManager m_manager;

    public SimulationScopeManagerTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IEwEController>(_ =>
        {
            var controller = new Mock<IEwEController>();
            var disposable = controller.As<IDisposable>();
            m_disposableControllers.Add(disposable);
            return controller.Object;
        });
        m_serviceProvider = services.BuildServiceProvider();
        m_manager = new SimulationScopeManager(m_serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SimulationScopeManager>.Instance);
    }

    [Fact]
    public void CreateSimulationScope_ReturnsServicesThatGetSimulationReturnsAgain()
    {
        // Arrange
        var created = m_manager.CreateSimulationScope("simulation");

        // Act
        var retrieved = m_manager.GetSimulation("simulation");

        // Assert
        retrieved.Should().BeSameAs(created);
    }

    [Fact]
    public void CreateSimulationScope_GivesEachSimulationItsOwnServices()
    {
        // Arrange
        // Act
        var first = m_manager.CreateSimulationScope("first");
        var second = m_manager.CreateSimulationScope("second");

        // Assert
        first.Should().NotBeSameAs(second);
        m_manager.GetSimulation("first").Should().BeSameAs(first);
        m_manager.GetSimulation("second").Should().BeSameAs(second);
    }

    [Fact]
    public void CreateSimulationScope_ThrowsAlreadyExists_WhenSimulationIsAlreadyRunning()
    {
        // Arrange
        var existing = m_manager.CreateSimulationScope("simulation");

        // Act
        var action = () => m_manager.CreateSimulationScope("simulation");

        // Assert
        action.Should().Throw<RpcException>().Which.StatusCode.Should().Be(StatusCode.AlreadyExists);
        m_manager.GetSimulation("simulation").Should().BeSameAs(existing);
    }

    [Fact]
    public void GetSimulation_ThrowsNotFound_WhenSimulationIsUnknown()
    {
        // Arrange
        // Act
        var action = () => m_manager.GetSimulation("unknown");

        // Assert
        action.Should().Throw<RpcException>().Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public void RemoveSimulationScope_DisposesOnlyThatSimulationsServices()
    {
        // Arrange
        m_manager.CreateSimulationScope("first");
        m_manager.CreateSimulationScope("second");

        // Act
        m_manager.RemoveSimulationScope("first");

        // Assert
        m_disposableControllers[0].Verify(disposable => disposable.Dispose(), Times.Once());
        m_disposableControllers[1].Verify(disposable => disposable.Dispose(), Times.Never());
        var getFirst = () => m_manager.GetSimulation("first");
        getFirst.Should().Throw<RpcException>().Which.StatusCode.Should().Be(StatusCode.NotFound);
        m_manager.GetSimulation("second").Should().NotBeNull();
    }

    [Fact]
    public void RemoveSimulationScope_DoesNothing_WhenSimulationIsUnknown()
    {
        // Arrange
        // Act
        var action = () => m_manager.RemoveSimulationScope("unknown");

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void CreateSimulationScope_AllowsReuseOfSimulationId_AfterRemoval()
    {
        // Arrange
        var first = m_manager.CreateSimulationScope("simulation");
        m_manager.RemoveSimulationScope("simulation");

        // Act
        var second = m_manager.CreateSimulationScope("simulation");

        // Assert
        second.Should().NotBeSameAs(first);
    }

    [Fact]
    public void Dispose_DisposesAllRemainingScopes()
    {
        // Arrange
        m_manager.CreateSimulationScope("first");
        m_manager.CreateSimulationScope("second");

        // Act
        m_manager.Dispose();

        // Assert
        m_disposableControllers.Should().HaveCount(2);
        m_disposableControllers.Should().AllSatisfy(disposable => disposable.Verify(d => d.Dispose(), Times.Once()));
    }

    public void Dispose()
    {
        m_manager.Dispose();
        m_serviceProvider.Dispose();
    }
}
