using EwECore;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathWorkflowService : WorkflowService.WorkflowServiceBase
{
    private readonly ILogger<EcopathWorkflowService> _logger;
    private readonly cCore _core;

    public EcopathWorkflowService(ILogger<EcopathWorkflowService> logger)
    {
        _logger = logger;
        _core = new cCore();
    }

    public override Task<InitResponse> Init(InitRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        Console.WriteLine($"Ecopath Initializing scenario {request.ScenarioId}...");

        return Task.FromResult(new InitResponse());
    }

    public override Task<UpdatePricesResponse> UpdatePrices(UpdatePricesRequest list, ServerCallContext context)
    {
        Console.WriteLine($"Updating prices for {list.Prices.Count} species...");

        // Simulate some processing delay
        // test Test
        //Task.Delay(1000).Wait();

        return Task.FromResult(new UpdatePricesResponse());
    }

    public override Task<SimulateStepResponse> SimulateStep(SimulateStepRequest req, ServerCallContext context)
    {
        Console.WriteLine($"Simulate step for simulation {req.SimulationId}");

        // Simulate some processing delay
        //Task.Delay(1000).Wait();

        return Task.FromResult(new SimulateStepResponse());
    }

    //private int InitEwE() Test
    //{
    //    // Load and configure EwE for a given model setup, and run Ecospace up to the reporting year. Then halt
        
    //    // - Model file? Ecosim scenario? Ecosim time series? Run length? Ecospace scenario? 
    //    // All this info as to be selected (directly or indirectly) by the user at some point
    //    string ModelName = @"Includes\Anchovy Bay Spatial.eiixml"; // ToDo: make this configurable
    //    int iEcosim = 1;
    //    int iTimeSeries = 0;
    //    int iEcospace = 1;
    
    //    // All core interactions should happen on a separate thread, non-blocking
    //    _core.PluginManager = new cPluginManager();
    //    Console.WriteLine("EwE loaded {0} plug-in(s)", _core.PluginManager.LoadPlugins());

    //    if (!_core.LoadModel(ModelName))
    //    {
    //        Console.WriteLine("EwE couldn't load model '{0}'", ModelName); // ToDo: log this properly
    //        return -1; // ToDo: throw a well defined error
    //    }
    //    Console.WriteLine("EwE loaded model '{0}'", ModelName); // ToDo: log this properly
        
    //    if (!_core.LoadEcosimScenario(1)) // ToDo: make this configurable
    //    {
    //        Console.WriteLine("EwE couldn't load ecosim scenario {0}", ModelName); // ToDo: log this properly
    //        return -1; // ToDo: throw a well defined error
    //    }        
    //    // etc
    //    Task.Delay(1000).Wait();
    //}
    
}
