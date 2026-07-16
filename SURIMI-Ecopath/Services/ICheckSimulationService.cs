using Grpc.Core;
using System.Runtime.CompilerServices;

namespace Ecopath.Services
{
    public interface ICheckSimulationService
    {
        Task CheckIfCorrectSimulationAsync(string simulationId, ServerCallContext context, [CallerMemberName] string methodName = "");
        void ReleaseSimulation(string simulationId);
        Task ReserveSimulationAsync(string simulationId, ServerCallContext context);
    }
}