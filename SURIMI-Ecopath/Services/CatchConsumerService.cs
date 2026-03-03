using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class CatchConsumerService : Grpc.Surimi.CatchConsumerService.CatchConsumerServiceBase
    {
        private readonly ILogger<CatchConsumerService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_ewecontroller;

        public CatchConsumerService(ILogger<CatchConsumerService> logger, CheckSimulationService service, IEwEController controller)
        {
            m_logger = logger;
            m_checksimulationservice = service;
            m_ewecontroller = controller;
        }

        public override async Task<UpdateCatchDispositionResponse> UpdateCatchDisposition(UpdateCatchDispositionRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("UpdateCatchDisposition", request.SimulationId, context);
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

            var catchDisposition = new SURIMI.Datamodel.CatchDispositionSummary
            {
                DispositionGrids = request.CatchDispositionSummary.DispositionGrids
                .Select(grid => new SURIMI.Datamodel.DispositionGrid
                {
                    FleetSegment = new SURIMI.Datamodel.FleetSegment
                    {
                        GearCode = grid.FleetSegment.GearCode,
                        CountryCode = grid.FleetSegment.CountryCode ?? string.Empty
                    },
                    Species = new SURIMI.Datamodel.Species
                    {
                        SpeciesCode = grid.Species.SpeciesCode,
                        LengthClass = grid.Species.LengthClass ?? string.Empty,
                        Age = grid.Species.Age ?? string.Empty,
                        LifeStage = grid.Species.LifeStage ?? string.Empty
                    },
                    DispositionCells = grid.DispositionCells
                        .Select(grpcCell => new SURIMI.Datamodel.DispositionCell
                        {
                            GrossCatchBiomass = grpcCell.GrossCatch,
                            LiveDiscardsBiomass = grpcCell.LiveDiscards,
                            DeadDiscardsBiomass = grpcCell.DeadDiscards,
                            Latitude = grpcCell.Latitude,
                            Longitude = grpcCell.Longitude
                        })
                        .ToList()
                })
                .ToList()
            };

            var res = await m_ewecontroller.UpdateCatchDispositionSummaryAsync(catchDisposition);

            return new UpdateCatchDispositionResponse() { SimulationId = request.SimulationId };
        }
    }
}
