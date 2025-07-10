using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathFisheryService : FisheryService.FisheryServiceBase
    {
        private readonly ILogger<EcopathEcologyService> _logger;
        private readonly CheckSimulationService _checkSimulationService;
        private readonly IEwEController _ewEController;

        public EcopathFisheryService(ILogger<EcopathEcologyService> logger, CheckSimulationService checkSimulationService, IEwEController ewEController)
        {
            _logger = logger;
            _checkSimulationService = checkSimulationService;
            _ewEController = ewEController;
        }

        public override async Task<GetCatchDispositionResponse> GetCatchDisposition(GetCatchDispositionRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("GetCatchDisposition", request.SimulationId, context);
            _logger.LogInformation($"Ecopath GetCatchDisposition for {request.SimulationId}...");

            var catchDisposition = await _ewEController.GetCatchDispositionSummaryAsync(
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
                            Species = new Species
                            {
                                SpeciesCode = grid.Species.SpeciesCode ?? string.Empty,
                                LengthClass = grid.Species.Length ?? string.Empty,
                                Age = grid.Species.Age ?? string.Empty,
                                Stage = grid.Species.Stage ?? string.Empty
                            },
                            FleetSegment = new FleetSegment
                            {
                                GearCode = grid.FleetSegment.GearCode ?? string.Empty,
                                Flag = grid.FleetSegment.flag ?? string.Empty
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
                    FleetSegment = new Ecopath.Models.FleetSegment
                    {
                        GearCode = grpcGrid.FleetSegment.GearCode,
                        flag = grpcGrid.FleetSegment.Flag
                    },
                    Species = new Ecopath.Models.Species
                    {
                        SpeciesCode = grpcGrid.Species.SpeciesCode
                    },
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

            var res = await _ewEController.UpdateCatchDispositionSummaryAsync(catchDisposition);

            return new UpdateCatchDispositionResponse() { SimulationId = request.SimulationId };
        }
    }
}
