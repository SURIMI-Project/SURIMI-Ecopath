using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathFisheryService : FisheryService.FisheryServiceBase
    {
        private readonly ILogger<EcopathEcologyService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_ewecontroller;

        public EcopathFisheryService(ILogger<EcopathEcologyService> logger, CheckSimulationService service, IEwEController controller)
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
                                Flag = grid.FleetSegment.Flag ?? string.Empty
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
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("UpdateCatchDisposition", request.SimulationId, context);
            GrpcValidation.ArgumentNotNullOrEmpty(request.CatchDispositionSummary.MeasurementUnit);

            var catchDisposition = new Ecopath.Models.CatchDispositionSummary
            {
                MeasurementUnit = request.CatchDispositionSummary.MeasurementUnit,
                DispositionGrids = request.CatchDispositionSummary.DispositionGrids
                .Select(grid => new Ecopath.Models.DispositionGrid
                {
                    FleetSegment = new Ecopath.Models.FleetSegment
                    {
                        GearCode = grid.FleetSegment.GearCode,
                        Flag = grid.FleetSegment.Flag ?? string.Empty
                    },
                    Species = new Ecopath.Models.Species
                    {
                        SpeciesCode = grid.Species.SpeciesCode,
                        Length = grid.Species.LengthClass ?? string.Empty,
                        Age = grid.Species.Age ?? string.Empty,
                        Stage = grid.Species.Stage ?? string.Empty
                    },
                    DispositionCells = grid.DispositionCells
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

            var res = await m_ewecontroller.UpdateCatchDispositionSummaryAsync(catchDisposition);

            return new UpdateCatchDispositionResponse() { SimulationId = request.SimulationId };
        }
    }
}
