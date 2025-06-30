using Grpc.Core;

namespace Ecopath.Services
{
    public class CheckSimulationService
    {
        private readonly ILogger<EcopathEcologyService> _logger;
        private static string _currentSimulationId = string.Empty;

        public CheckSimulationService(ILogger<EcopathEcologyService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// reserve Ecopath for this simulation. 
        /// </summary>
        /// <param name="simulationId"></param>
        public async Task ReserveSimulationAsync(string simulationId, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            var httpContext = context.GetHttpContext();
            var ipAddress = httpContext.Connection.LocalIpAddress?.ToString() ?? "localhost";
            var port = httpContext.Connection.LocalPort;
            var scheme = httpContext.Request.Scheme;
            var hostHeader = $"{scheme}://{ipAddress}:{port}";


            if (!string.IsNullOrEmpty(_currentSimulationId))
            {
                throw new RpcException(new Status(StatusCode.Unavailable, $"Cannot reserve for simulation '{simulationId}' on host {hostHeader}. Ecopath already reserved for simulation '{_currentSimulationId}'."));
            }
            _currentSimulationId = simulationId;

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { "host", hostHeader }
            });
            _logger.LogInformation($"Simulation with '{simulationId}' is reserved on host: {hostHeader}");
        }

        /// <summary>
        /// Release Ecopath for this simulation.
        /// </summary>
        /// <param name="simulationId"></param>
        public void ReleaseSimulation(string simulationId)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            if (_currentSimulationId != simulationId)
            {
                _logger.LogInformation($"Attempted to release simulation '{simulationId}', but current simulation is '{_currentSimulationId}'. No action taken.");
                return;
            }
            _currentSimulationId = string.Empty;
        }

        public async Task CheckIfCorrectSimulationAsync(string methodName, string simulationId, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            var httpContext = context.GetHttpContext();
            var ipAddress = httpContext.Connection.LocalIpAddress?.ToString() ?? "localhost";
            var port = httpContext.Connection.LocalPort;
            var scheme = httpContext.Request.Scheme;
            var hostHeader = $"{scheme}://{ipAddress}:{port}";

            if (string.IsNullOrEmpty(_currentSimulationId) || _currentSimulationId != simulationId)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, $"Ecopath is not reserved for simulation '{simulationId}' on host {hostHeader}. It's reserved for '{_currentSimulationId}'"));
            }

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { "host", hostHeader }
            });
            _logger.LogInformation($"{methodName} called with '{simulationId}' from '{hostHeader}'");
        }
    }
}
