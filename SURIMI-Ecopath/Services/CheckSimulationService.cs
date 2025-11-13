using Grpc.Core;

namespace Ecopath.Services
{
    public class CheckSimulationService
    {
        private readonly ILogger<EcopathEcologyService> m_logger;
        private static string m_simulationId = string.Empty;

        public CheckSimulationService(ILogger<EcopathEcologyService> logger)
        {
            m_logger = logger;
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


            if (!string.IsNullOrEmpty(m_simulationId))
            {
                throw new RpcException(new Status(StatusCode.Unavailable, $"Cannot reserve for simulation '{simulationId}' on host {hostHeader}. DnsName: {System.Net.Dns.GetHostName()}. Ecopath already reserved for simulation '{m_simulationId}'."));
            }
            m_simulationId = simulationId;

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { "host", hostHeader }
            });
            m_logger.LogInformation($"Simulation with '{simulationId}' is reserved on host: {hostHeader}");
        }

        /// <summary>
        /// Release Ecopath for this simulation.
        /// </summary>
        /// <param name="simulationId"></param>
        public void ReleaseSimulation(string simulationId)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            if (m_simulationId != simulationId)
            {
                m_logger.LogInformation($"Attempted to release simulation '{simulationId}', but current simulation is '{m_simulationId}'. No action taken.");
                return;
            }
            m_simulationId = string.Empty;
        }

        public async Task CheckIfCorrectSimulationAsync(string methodName, string simulationId, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            var httpContext = context.GetHttpContext();
            var ipAddress = httpContext.Connection.LocalIpAddress?.ToString() ?? "localhost";
            var port = httpContext.Connection.LocalPort;
            var scheme = httpContext.Request.Scheme;
            var hostHeader = $"{scheme}://{ipAddress}:{port}";

            if (string.IsNullOrEmpty(m_simulationId) || m_simulationId != simulationId)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, $"Ecopath is not reserved for simulation '{simulationId}' on host {hostHeader}. It's reserved for '{m_simulationId}'"));
            }

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { "host", hostHeader }
            });
            m_logger.LogInformation($"{methodName} called with '{simulationId}' from '{hostHeader}'");
        }
    }
}
