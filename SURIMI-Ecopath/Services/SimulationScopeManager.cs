using System.Collections.Concurrent;
using Ecopath.EwE;
using Grpc.Core;
using SURIMI.Common.gRPC;

namespace Ecopath.Services;

/// <summary>
/// Singleton that manages one dependency injection scope per simulation.
/// Each scope owns its own EwE core, controller, configuration and HCR state.
/// </summary>
public sealed class SimulationScopeManager : ISimulationScopeManager, IDisposable
{
    private readonly IServiceScopeFactory m_serviceScopeFactory;
    private readonly ILogger<SimulationScopeManager> m_logger;
    private readonly ConcurrentDictionary<string, IServiceScope> m_scopes = new();

    public SimulationScopeManager(IServiceScopeFactory serviceScopeFactory, ILogger<SimulationScopeManager> logger)
    {
        m_serviceScopeFactory = serviceScopeFactory;
        m_logger = logger;
    }

    /// <inheritdoc />
    public IEwEController CreateSimulationScope(string simulationId)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(simulationId);

        var scope = m_serviceScopeFactory.CreateScope();
        if (!m_scopes.TryAdd(simulationId, scope))
        {
            scope.Dispose();
            throw new RpcException(new Status(StatusCode.AlreadyExists, $"Simulation '{simulationId}' is already running"));
        }

        m_logger.LogInformation("Created scope for simulation {SimulationId}. Active simulations: {ActiveSimulations}", simulationId, m_scopes.Count);
        return scope.ServiceProvider.GetRequiredService<IEwEController>();
    }

    /// <inheritdoc />
    public IEwEController GetSimulation(string simulationId)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(simulationId);

        if (!m_scopes.TryGetValue(simulationId, out var scope))
            throw new RpcException(new Status(StatusCode.NotFound, $"Simulation '{simulationId}' is not running"));

        return scope.ServiceProvider.GetRequiredService<IEwEController>();
    }

    /// <inheritdoc />
    public void RemoveSimulationScope(string simulationId)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(simulationId);

        if (!m_scopes.TryRemove(simulationId, out var scope))
        {
            m_logger.LogInformation("No scope to remove for simulation {SimulationId}", simulationId);
            return;
        }

        scope.Dispose();
        m_logger.LogInformation("Removed scope for simulation {SimulationId}. Active simulations: {ActiveSimulations}", simulationId, m_scopes.Count);
    }

    /// <summary>
    /// Disposes the scopes of all simulations that are still running, for example when the host shuts down.
    /// </summary>
    public void Dispose()
    {
        foreach (var simulationId in m_scopes.Keys)
            RemoveSimulationScope(simulationId);
    }
}
