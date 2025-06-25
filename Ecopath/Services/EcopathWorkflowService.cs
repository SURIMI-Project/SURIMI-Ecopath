using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathWorkflowService : WorkflowService.WorkflowServiceBase
{
    private readonly ILogger<EcopathWorkflowService> _logger;
    private readonly CheckSimulationService _checkSimulationService;
    private readonly SimulationService _simulationService;

    public EcopathWorkflowService(ILogger<EcopathWorkflowService> logger, SimulationService simulationService, CheckSimulationService checkSimulationService)
    {
        _logger = logger;
        _simulationService = simulationService;
        _checkSimulationService = checkSimulationService;
    }

    public override async Task<InitialiseResponse> Initialise(InitialiseRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        await _checkSimulationService.ReserveSimulationAsync(request.SimulationId, context);
        _logger.LogInformation($"Initializing simulation {request.SimulationId}, scenario {request.ScenarioId}...");

        try
        {
            var result = await _simulationService.InitAsync(request.SimulationId, request.ScenarioId, request.StartDateTime.ToDateTime(), request.StepSize);
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to initialise Ecopath"));
            }
            return new InitialiseResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public override Task<FinaliseResponse> Finalise(FinaliseRequest request, ServerCallContext context)
    {
        _checkSimulationService.ReleaseSimulation(request.SimulationId);
        _logger.LogInformation($"Finalizing simulation {request.SimulationId}");

        try
        {
            // TODO

            //var result = await _simulationService.FinaliseAsync(request.SimulationId, request.ScenarioId, request.StartDateTime.ToDateTime(), request.StepSize);
            //if (result == false)
            //{
            //    throw new RpcException(new Status(StatusCode.Internal, "Failed to finalise Ecopath"));
            //}
            return Task.FromResult(new FinaliseResponse() { SimulationId = request.SimulationId });
        }
        catch (Exception ex)
        {
            throw;
        }
    }


    public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
    {
        await _checkSimulationService.CheckIfCorrectSimulationAsync("SimulateStep", request.SimulationId, context);
        _logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

        var res = await _simulationService.ContinueAsync(request.SimulationId);

        return new SimulateStepResponse();
    }
}
