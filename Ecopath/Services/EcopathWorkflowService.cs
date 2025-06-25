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

    public override async Task<InitResponse> Init(InitRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        await _checkSimulationService.ReserveSimulationAsync(request.SimulationId, context);
        _logger.LogInformation($"Initializing simulation {request.SimulationId}, scenario {request.ScenarioId}...");

        try
        {
            var result = await _simulationService.InitAsync(request.SimulationId, request.ScenarioId, request.StartDateTime.ToDateTime(), request.StepSize);
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to initialize Ecopath"));
            }
            return new InitResponse();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public override Task<FinalizeResponse> Finalize(FinalizeRequest request, ServerCallContext context)
    {
        _checkSimulationService.ReleaseSimulation(request.SimulationId);
        _logger.LogInformation($"Finalizing simulation {request.SimulationId}");

        try
        {
            // TODO

            //var result = await _simulationService.FinalizeAsync(request.SimulationId, request.ScenarioId, request.StartDateTime.ToDateTime(), request.StepSize);
            //if (result == false)
            //{
            //    throw new RpcException(new Status(StatusCode.Internal, "Failed to finalize Ecopath"));
            //}
            return Task.FromResult(new FinalizeResponse());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public override async Task<UpdatePricesResponse> UpdatePrices(UpdatePricesRequest request, ServerCallContext context)
    {
        await _checkSimulationService.CheckIfCorrectSimulationAsync("UpdatePrices", request.SimulationId, context);
        _logger.LogInformation($"Updating prices for {request.Prices.Count} species...");

        var speciesPrices = request.Prices
            .Select(p => new Models.SpeciesPrice { 
                SpeciesCode = p.SpeciesCode, 
                Price = p.Price, 
                Currency = p.Currency, 
                MeasuremenyUnit = p.MeasurementUnit,
                PortCode = p.PortCode,
                Timestamp = p.Timestamp.ToDateTime()
            }) 
            .ToList();

        var res = await _simulationService.UpdatePricesAsync(request.SimulationId, speciesPrices);

        return new UpdatePricesResponse();
    }

    public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
    {
        await _checkSimulationService.CheckIfCorrectSimulationAsync("SimulateStep", request.SimulationId, context);
        _logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

        var res = await _simulationService.ContinueAsync(request.SimulationId);

        return new SimulateStepResponse();
    }
}
