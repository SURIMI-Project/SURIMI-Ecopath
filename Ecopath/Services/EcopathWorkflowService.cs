using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathWorkflowService : WorkflowService.WorkflowServiceBase
{
    private readonly ILogger<EcopathWorkflowService> _logger;
    private readonly SimulationService _simulationService;

    public EcopathWorkflowService(ILogger<EcopathWorkflowService> logger, SimulationService simulationService)
    {
        _logger = logger;
        _simulationService = simulationService;
    }

    public override async Task<InitResponse> Init(InitRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
        _logger.LogInformation($"Ecopath Initializing scenario {request.ScenarioId}...");

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

    public override async Task<UpdatePricesResponse> UpdatePrices(UpdatePricesRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
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
        GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
        _logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

        var res = await _simulationService.ContinueAsync(request.SimulationId);

        return new SimulateStepResponse();
    }
}
