using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathWorkflowService : WorkflowService.WorkflowServiceBase
{
    private readonly ILogger<EcopathWorkflowService> _logger;
    private readonly CheckSimulationService _checkSimulationService;
    private readonly IEwEController _ewEController;

    public EcopathWorkflowService(ILogger<EcopathWorkflowService> logger, CheckSimulationService checkSimulationService, IEwEController ewEController)
    {
        _logger = logger;
        _checkSimulationService = checkSimulationService;
        _ewEController = ewEController;
    }

    public override async Task<InitialiseResponse> Initialise(InitialiseRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        await _checkSimulationService.ReserveSimulationAsync(request.SimulationId, context);


        _logger.LogInformation($"Initializing simulation {request.SimulationId}, scenario {request.ScenarioId}...");

        try
        {
            var result = await _ewEController.StartAsync(new EwEConfiguration());
            if (result != 1)
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

    public override async Task<FinaliseResponse> Finalise(FinaliseRequest request, ServerCallContext context)
    {
        _checkSimulationService.ReleaseSimulation(request.SimulationId);
        _logger.LogInformation($"Finalizing simulation {request.SimulationId}");

        try
        {
            var result = await _ewEController.StopAsync();
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to finalise Ecopath"));
            }
            return new FinaliseResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public override async Task<CancelResponse> Cancel(CancelRequest request, ServerCallContext context)
    {
        _checkSimulationService.ReleaseSimulation(request.SimulationId);
        _logger.LogInformation($"Cancel simulation {request.SimulationId}");

        try
        {
            var result = await _ewEController.StopAsync();
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to cancel Ecopath"));
            }
            return new CancelResponse() { SimulationId = request.SimulationId };
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

        var res = await _ewEController.ContinueAsync();

        return new SimulateStepResponse() { SimulationId = request.SimulationId };
    }
}
