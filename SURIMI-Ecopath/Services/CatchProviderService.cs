using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class CatchProviderService : Grpc.Surimi.CatchProviderService.CatchProviderServiceBase
    {
        private readonly ILogger<CatchProviderService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_ewecontroller;

        public CatchProviderService(ILogger<CatchProviderService> logger, CheckSimulationService service, IEwEController controller)
        {
            m_logger = logger;
            m_checksimulationservice = service;
            m_ewecontroller = controller;
        }

        public override async Task<GetCatchDispositionResponse> GetCatchDisposition(GetCatchDispositionRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("GetCatchDisposition", request.SimulationId, context);
            m_logger.LogInformation($"Ecopath GetCatchDisposition for {request.SimulationId}...");

            var catchDisposition = await m_ewecontroller.GetCatchDispositionSummaryAsync(
                request.StartDateTime.ToDateTime(),
                request.EndDateTime.ToDateTime()
            );

            var response = new GetCatchDispositionResponse
            {
                CatchDispositionSummary = new CatchDispositionSummary(),
                SimulationId = request.SimulationId,
                StartDateTime = request.StartDateTime,
                EndDateTime = request.EndDateTime
            };

            if (catchDisposition?.DispositionGrids != null)
            {
                response.CatchDispositionSummary.DispositionGrids.AddRange(
                    catchDisposition.DispositionGrids.Select(grid =>
                    {
                        var dispositionGrid = new DispositionGrid
                        {
                            Species = new Species
                            {
                                SpeciesCode = grid.Species.SpeciesCode ?? string.Empty,
                                LengthClass = grid.Species.LengthClass ?? string.Empty,
                                Age = grid.Species.Age ?? string.Empty,
                                LifeStage = grid.Species.LifeStage ?? string.Empty
                            },
                            FleetSegment = new FleetSegment
                            {
                                GearCode = grid.FleetSegment.GearCode ?? string.Empty,
                                CountryCode = grid.FleetSegment.CountryCode ?? string.Empty
                            }
                        };
                        if (grid.DispositionCells != null)
                        {
                            dispositionGrid.DispositionCells.AddRange(
                                grid.DispositionCells.Select(cell => new DispositionCell
                                {
                                    GrossCatch = cell.GrossCatchBiomass,
                                    LiveDiscards = cell.LiveDiscardsBiomass,
                                    DeadDiscards = cell.DeadDiscardsBiomass,
                                    Latitude = cell.Latitude,
                                    Longitude = cell.Longitude
                                })
                            );
                        }
                        return dispositionGrid;
                    })
                );
            }

            return response;
        }
    }
}
