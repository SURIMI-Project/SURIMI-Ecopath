using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathWorkflowService : WorkflowService.WorkflowServiceBase
{
    private readonly ILogger<EcopathWorkflowService> _logger;
    private readonly EwEController _MEMcontroller;

    public EcopathWorkflowService(ILogger<EcopathWorkflowService> logger, EwEController ewEcontroller)
    {
        _logger = logger;
        _MEMcontroller = ewEcontroller;
    }

    public override async Task<InitResponse> Init(InitRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        Console.WriteLine($"Ecopath Initializing scenario {request.ScenarioId}...");

        try
        {
            var result = await _MEMcontroller.StartAsync();
            if (result < 0)
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

    public override Task<UpdatePricesResponse> UpdatePrices(UpdatePricesRequest list, ServerCallContext context)
    {
        Console.WriteLine($"Updating prices for {list.Prices.Count} species...");

        // Simulate some processing delay
        //Task.Delay(1000).Wait();

        return Task.FromResult(new UpdatePricesResponse());
    }

    public override Task<SimulateStepResponse> SimulateStep(SimulateStepRequest req, ServerCallContext context)
    {
        Console.WriteLine($"Simulate step for simulation {req.SimulationId}");

        _MEMcontroller.Continue();
        // Simulate some processing delay
        //Task.Delay(1000).Wait();

        return Task.FromResult(new SimulateStepResponse());
    }
}
