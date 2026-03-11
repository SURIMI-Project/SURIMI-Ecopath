using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class RegulationsConsumerService : Grpc.Surimi.RegulationsConsumerService.RegulationsConsumerServiceBase
    {
        private readonly ILogger<RegulationsConsumerService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_controller;

        public RegulationsConsumerService(ILogger<RegulationsConsumerService> logger, CheckSimulationService service, IEwEController controller)
        {
            m_logger = logger;
            m_checksimulationservice = service;
            m_controller = controller;
        }

        public override async Task<UpdateRegulationsResponse> UpdateRegulations(UpdateRegulationsRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("UpdateRegulations", request.SimulationId, context);
            m_logger.LogInformation($"Updating Regulations for Simulation {request.SimulationId} ");

            var regulations = new SURIMI.Datamodel.RegulationsSummary
            {
                TotalAllowableCatches = request.RegulationsSummary.TotalAllowableCatches
                    .Select(tac => new SURIMI.Datamodel.TotalAllowableCatch
                    {
                        FleetSegment = new SURIMI.Datamodel.FleetSegment
                        {
                            GearCode = tac.FleetSegment.GearCode,
                            CountryCode = tac.FleetSegment.CountryCode ?? string.Empty
                        },
                        Species = new SURIMI.Datamodel.Species
                        {
                            SpeciesCode = tac.Species.SpeciesCode,
                            LengthClass = tac.Species.LengthClass ?? string.Empty,
                            Age = tac.Species.Age ?? string.Empty,
                            LifeStage = tac.Species.LifeStage ?? string.Empty
                        },
                    })
                    .ToList()
            };

            var res = await m_controller.UpdateRegulationsAsync(regulations);

            return new UpdateRegulationsResponse() { SimulationId = request.SimulationId };
        }

        public override async Task<GetFishingActivityResponse> GetFishingActivity(GetFishingActivityRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("GetFishingActivity", request.SimulationId, context);
            m_logger.LogInformation($"Getting Fishing Activity for Simulation {request.SimulationId}...");
            
            var fishingActivity = await m_controller.GetFishingActivityAsync();

            var fishingActivityGrpc = new FishingActivitySummary
            {
                FishingActivities = { fishingActivity.FishingActivities.Select(activity => new FishingActivity
                {
                    FleetSegment = new FleetSegment
                    {
                        GearCode = activity.FleetSegment?.GearCode ?? string.Empty,
                        CountryCode = activity.FleetSegment?.CountryCode ?? string.Empty
                    },
                    FishingActivityRatio = activity.FishingActivityRatio
                })}
            };

            var response = new GetFishingActivityResponse
            {
                StartDateTime = request.StartDateTime,
                EndDateTime = request.EndDateTime,
                SimulationId = request.SimulationId,
                FishingActivitySummary = fishingActivityGrpc
            };

            return response;
        }
    }
}