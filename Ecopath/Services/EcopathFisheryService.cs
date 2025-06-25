using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathFisheryService : FisheryService.FisheryServiceBase
    {
        private readonly ILogger<EcopathEcologyService> _logger;
        private readonly SimulationService _simulationService;
        private readonly CheckSimulationService _checkSimulationService;

        public EcopathFisheryService(ILogger<EcopathEcologyService> logger, SimulationService simulationService, CheckSimulationService checkSimulationService)
        {
            _logger = logger;
            _simulationService = simulationService;
            _checkSimulationService = checkSimulationService;
        }

        public override async Task<GetCatchDispositionResponse> GetCatchDisposition(GetCatchDispositionRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("GetCatchDisposition", request.SimulationId, context);
            _logger.LogInformation($"Ecopath GetCatchDisposition for {request.SimulationId}...");

            var catchDisposition = await _simulationService.GetCatchDispositionAsync(
                request.SimulationId,
                request.StartDateTime.ToDateTime(),
                request.EndDateTime.ToDateTime()
            );

            var response = new GetCatchDispositionResponse
            {
                CatchDispositionSummary = new CatchDispositionSummary
                {
                    MeasurementUnit = catchDisposition?.MeasurementUnit ?? string.Empty
                },
                SimulationId = request.SimulationId
            };

            if (catchDisposition?.DispositionGrids != null)
            {
                response.CatchDispositionSummary.DispositionGrids.AddRange(
                    catchDisposition.DispositionGrids.Select(grid =>
                    {
                        var dispositionGrid = new DispositionGrid
                        {
                            SpeciesCode = grid.SpeciesCode,
                            GearCode = grid.GearCode,
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

        public override async Task<UpdateCatchDispositionResponse> UpdateCatchDisposition(UpdateCatchDispositionRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("UpdateCatchDisposition", request.SimulationId, context);
            GrpcValidation.ArgumentNotNullOrEmpty(request.CatchDispositionSummary.MeasurementUnit);

            var catchDisposition = new Ecopath.Models.CatchDispositionSummary
            {
                MeasurementUnit = request.CatchDispositionSummary.MeasurementUnit,
                DispositionGrids = request.CatchDispositionSummary.DispositionGrids
                .Select(grpcGrid => new Ecopath.Models.DispositionGrid
                {
                    GearCode = grpcGrid.GearCode,
                    SpeciesCode = grpcGrid.SpeciesCode,
                    DispositionCells = grpcGrid.DispositionCells
                        .Select(grpcCell => new Models.DispositionCell
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

            var res = await _simulationService.UpdateCatchDisposition(request.SimulationId, catchDisposition);

            return new UpdateCatchDispositionResponse() { SimulationId = request.SimulationId };
        }
    }
}
