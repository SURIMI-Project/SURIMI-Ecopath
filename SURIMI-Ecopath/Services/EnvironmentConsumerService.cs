using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EnvironmentConsumerService : Grpc.Surimi.EnvironmentConsumerService.EnvironmentConsumerServiceBase
    {
        private readonly ILogger<EnvironmentConsumerService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_controller;

        public EnvironmentConsumerService(ILogger<EnvironmentConsumerService> logger, CheckSimulationService service, IEwEController controller)
        {
            m_logger = logger;
            m_checksimulationservice = service;
            m_controller = controller;
        }

        public override async Task<UpdateEnvironmentVariablesResponse> UpdateEnvironmentVariables(UpdateEnvironmentVariablesRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("UpdateEnvironmentVariables", request.SimulationId, context);
            m_logger.LogInformation($"Updating environment variables for {request.EnvironmentVariablesSummary.EnvironmentVariablesGrids.Count} variables...");

            var environmentVariables = new SURIMI.Datamodel.EnvironmentVariablesSummary
            {
                EnvironmentVariablesGrids = request.EnvironmentVariablesSummary.EnvironmentVariablesGrids
                .Select(grid => new SURIMI.Datamodel.EnvironmentVariablesGrid
                {
                    VariableCode = grid.VariableCode,
                    EnvironmentVariablesCells = grid.EnvironmentVariablesCells
                        .Select(cell => new SURIMI.Datamodel.EnvironmentVariablesCell
                        {
                            Longitude = cell.Longitude,
                            Latitude = cell.Latitude,
                            Value = cell.Value
                        })
                        .ToList()
                })
                .ToList()
            };

            var res = await m_controller.UpdateEnvironmentVariablesAsync(environmentVariables);

            return new UpdateEnvironmentVariablesResponse() { SimulationId = request.SimulationId };
        }
    }
}