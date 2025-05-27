using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathFisheryService : FisheryService.FisheryServiceBase
    {
        private readonly ILogger<EcopathEcologyService> _logger;
        private readonly SimulationService _simulationService;

        public EcopathFisheryService(ILogger<EcopathEcologyService> logger, SimulationService simulationService)
        {
            _logger = logger;
            _simulationService = simulationService;
        }

        public override async Task<GetSalesSummaryResponse> GetSalesSummary(GetSalesSummaryRequest request, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
            _logger.LogInformation($"Ecopath GetSalesSummary for {request.SimulationId}...");

            var salesSummaries = await _simulationService.GetSalesSummariesAsync(request.SimulationId, request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime());

            var response = new GetSalesSummaryResponse();
            response.SalesSummaries.AddRange(
                salesSummaries.Select(summary =>
                {
                    var grpcSummary = new SalesSummary
                    {
                        MarketCode = summary.MarketId,
                        MeasurementUnit = summary.MeasurementUnit,
                        Currency = summary.Currency
                    };

                    if (summary.Sales != null)
                    {
                        grpcSummary.Sales.AddRange(summary.Sales.Select(sale => new Sale
                        {
                            SpeciesCode = sale.SpeciesCode,
                            Quantity = sale.Quantity,
                            Value = sale.Value,
                        }));
                    }

                    return grpcSummary;
                })
            );

            return response;
        }

        public override async Task<GetCatchDispositionSummaryResponse> GetCatchDispositionSummary(GetCatchDispositionSummaryRequest request, ServerCallContext context)
        {
            GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
            _logger.LogInformation($"Ecopath GetCatchDispositionSummary for {request.SimulationId}...");

            var catchDispositionSummary = await _simulationService.GetCatchDispositionSummaryAsync(
                request.SimulationId,
                request.StartDateTime.ToDateTime(),
                request.EndDateTime.ToDateTime()
            );

            var response = new GetCatchDispositionSummaryResponse
            {
                MeasurementUnit = catchDispositionSummary?.MeasurementUnit
            };

            if (catchDispositionSummary?.DispositionGrids != null)
            {
                response.DispositionGrids.AddRange(
                    catchDispositionSummary.DispositionGrids.Select(grid =>
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
    }
}
