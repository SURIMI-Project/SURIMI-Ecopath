using Ecopath.EwE;
using Grpc.Core;

namespace Ecopath.Services;

/// <summary>
/// Manages one dependency injection scope per simulation, so that simulations running in the same process do not share state.
/// </summary>
public interface ISimulationScopeManager
{
    /// <summary>
    /// Creates the scope for <paramref name="simulationId"/>.
    /// </summary>
    /// <exception cref="RpcException">Thrown with <see cref="StatusCode.AlreadyExists"/> when a scope exists for the simulation.</exception>
    IEwEController CreateSimulationScope(string simulationId);

    /// <summary>
    /// Gets the controller of the scope belonging to <paramref name="simulationId"/>.
    /// </summary>
    /// <exception cref="RpcException">Thrown with <see cref="StatusCode.NotFound"/> when no scope exists for the simulation.</exception>
    IEwEController GetSimulation(string simulationId);

    /// <summary>
    /// Disposes the scope belonging to <paramref name="simulationId"/>, releasing the EwE engine and everything else the simulation owns.
    /// Does nothing when no scope exists for the simulation.
    /// </summary>
    void RemoveSimulationScope(string simulationId);
}
