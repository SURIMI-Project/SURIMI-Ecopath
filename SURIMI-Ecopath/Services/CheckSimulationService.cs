using Grpc.Core;
using SURIMI.Common.gRPC;
using System.Runtime.CompilerServices;

namespace Ecopath.Services
{
    /// <summary>
    /// Manages exclusive reservation of the Ecopath engine for a single simulation at a time.
    /// Throws <see cref="RpcException"/> with <see cref="StatusCode.Unavailable"/> and an
    /// <c>active-simulation-id</c> trailer when the engine is already reserved by another simulation.
    /// </summary>
    public class CheckSimulationService : ICheckSimulationService
    {
        private readonly ILogger<ICheckSimulationService> m_logger;
        private readonly Lock m_lock = new();       // Lock to ensure thread-safe access to m_simulationId
        private string m_simulationId = string.Empty;

        public CheckSimulationService(ILogger<ICheckSimulationService> logger)
        {
            m_logger = logger;
        }

        /// <summary>
        /// Reserves the Ecopath engine exclusively for <paramref name="simulationId"/>.
        /// Writes the resolved host address as a <c>host</c> response header on success.
        /// </summary>
        /// <param name="simulationId">The unique identifier of the simulation claiming the reservation.</param>
        /// <param name="context">The active gRPC server call context, used to resolve the host address and write response headers.</param>
        /// <exception cref="RpcException">
        /// Thrown with <see cref="StatusCode.Unavailable"/> when the engine is already reserved for a different simulation.
        /// The <c>active-simulation-id</c> trailer contains the ID of the simulation currently holding the reservation.
        /// </exception>
        public async Task ReserveSimulationAsync(string simulationId, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            var hostHeader = GetHostHeader(context);

            lock (m_lock)
            {
                if (!string.IsNullOrEmpty(m_simulationId))
                    throw CreateUnavailableException($"Cannot reserve for simulation '{simulationId}' on host {hostHeader}. DnsName: {System.Net.Dns.GetHostName()}. Ecopath already reserved for simulation '{m_simulationId}'.");
                m_simulationId = simulationId;
            }

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { "host", hostHeader }
            });
            m_logger.LogInformation($"Simulation with '{simulationId}' is reserved on host: {hostHeader}");
        }

        /// <summary>
        /// Releases the reservation held by <paramref name="simulationId"/>, allowing a new simulation to reserve the engine.
        /// If <paramref name="simulationId"/> does not match the currently reserved simulation, no action is taken.
        /// </summary>
        /// <param name="simulationId">The unique identifier of the simulation releasing the reservation.</param>
        public void ReleaseSimulation(string simulationId)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            lock (m_lock)
            {
                if (m_simulationId != simulationId)
                {
                    m_logger.LogInformation($"Attempted to release simulation '{simulationId}', but current simulation is '{m_simulationId}'. No action taken.");
                    return;
                }
                m_simulationId = string.Empty;
            }
        }

        /// <summary>
        /// Verifies that the Ecopath engine is currently reserved for <paramref name="simulationId"/>.
        /// Writes the resolved host address as a <c>host</c> response header on success.
        /// </summary>
        /// <param name="simulationId">The unique identifier of the simulation that must hold the reservation.</param>
        /// <param name="context">The active gRPC server call context, used to resolve the host address and write response headers.</param>
        /// <param name="methodName">The name of the calling gRPC method, captured automatically via <see cref="CallerMemberNameAttribute"/>.</param>
        /// <exception cref="RpcException">
        /// Thrown with <see cref="StatusCode.Unavailable"/> when the engine is not reserved, or is reserved for a different simulation.
        /// The <c>active-simulation-id</c> trailer contains the ID of the simulation currently holding the reservation.
        /// </exception>
        public async Task CheckIfCorrectSimulationAsync(string simulationId, ServerCallContext context, [CallerMemberName] string methodName = "")
        {
            GrpcValidation.ArgumentNotNullOrEmpty(simulationId);
            var hostHeader = GetHostHeader(context);

            lock (m_lock)
            {
                if (string.IsNullOrEmpty(m_simulationId) || m_simulationId != simulationId)
                    throw CreateUnavailableException($"Ecopath is not reserved for simulation '{simulationId}' on host {hostHeader}. It's reserved for '{m_simulationId}'");
            }

            await context.WriteResponseHeadersAsync(new Metadata
            {
                { "host", hostHeader }
            });
            m_logger.LogInformation($"{methodName} called with '{simulationId}' from '{hostHeader}'");
        }

        /// <summary>
        /// Builds a host URL string (scheme://ip:port) from the local endpoint of the active gRPC call.
        /// </summary>
        private static string GetHostHeader(ServerCallContext context)
        {
            var httpContext = context.GetHttpContext();
            var ipAddress = httpContext.Connection.LocalIpAddress?.ToString() ?? "localhost";
            var port = httpContext.Connection.LocalPort;
            var scheme = httpContext.Request.Scheme;
            return $"{scheme}://{ipAddress}:{port}";
        }

        /// <summary>
        /// Creates an <see cref="RpcException"/> with <see cref="StatusCode.Unavailable"/> and attaches the
        /// currently reserved simulation ID as an <c>active-simulation-id</c> trailer.
        /// </summary>
        private RpcException CreateUnavailableException(string message)
        {
            var trailers = new Metadata { { "active-simulation-id", m_simulationId } };
            return new RpcException(new Status(StatusCode.Unavailable, message), trailers);
        }
    }
}
