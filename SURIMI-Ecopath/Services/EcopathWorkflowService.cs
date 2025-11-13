using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathWorkflowService : WorkflowService.WorkflowServiceBase
{
    private readonly ILogger<EcopathWorkflowService> m_logger;
    private readonly CheckSimulationService m_checksimulationservice;
    private readonly IEwEController m_controller;

    public EcopathWorkflowService(ILogger<EcopathWorkflowService> logger, CheckSimulationService service, IEwEController controller)
    {
        m_logger = logger;
        m_checksimulationservice = service;
        m_controller = controller;
    }

    public override async Task<InitialiseResponse> Initialise(InitialiseRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioId);
        await m_checksimulationservice.ReserveSimulationAsync(request.SimulationId, context);


        m_logger.LogInformation($"Initializing simulation {request.SimulationId}, scenario {request.ScenarioId}...");

        try
        {
            var result = await m_controller.StartAsync();
            if (result != 1)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to initialise Ecopath"));
            }
            return new InitialiseResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            m_logger.LogInformation("In Initialise. EwE - exception ...{Message}", ex.Message);
            throw;
        }
    }

    public override async Task<FinaliseResponse> Finalise(FinaliseRequest request, ServerCallContext context)
    {
        m_checksimulationservice.ReleaseSimulation(request.SimulationId);
        m_logger.LogInformation($"Finalizing simulation {request.SimulationId}");

        try
        {
            var result = await m_controller.StopAsync();
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to finalise Ecopath"));
            }
            return new FinaliseResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            m_logger.LogInformation("In Finalise. - exception ...{Message}", ex.Message);
            throw;
        }
    }

    public override async Task<CancelResponse> Cancel(CancelRequest request, ServerCallContext context)
    {
        m_checksimulationservice.ReleaseSimulation(request.SimulationId);
        m_logger.LogInformation($"Cancel simulation {request.SimulationId}");

        try
        {
            var result = await m_controller.StopAsync();
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to cancel Ecopath"));
            }
            return new CancelResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            m_logger.LogInformation("In Cancel. - exception ...{Message}", ex.Message);
            throw;
        }
    }

    public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync("SimulateStep", request.SimulationId, context);
        m_logger.LogInformation($"Simulate step for simulation {request.SimulationId}");

        var res = await m_controller.ContinueAsync();

        return new SimulateStepResponse() { SimulationId = request.SimulationId };
    }
}
