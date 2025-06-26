using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathMarketService : MarketService.MarketServiceBase
    {
        private readonly ILogger<EcopathEcologyService> _logger;
        private readonly SimulationService _simulationService;
        private readonly CheckSimulationService _checkSimulationService;

        public EcopathMarketService(ILogger<EcopathEcologyService> logger, SimulationService simulationService, CheckSimulationService checkSimulationService)
        {
            _logger = logger;
            _simulationService = simulationService;
            _checkSimulationService = checkSimulationService;
        }

        public override async Task<UpdateSpeciesPricesResponse> UpdateSpeciesPrices(UpdateSpeciesPricesRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("UpdateSpeciesPrices", request.SimulationId, context);
            _logger.LogInformation($"Updating prices for {request.Prices.Count} species...");

            var speciesPrices = request.Prices
                .Select(p => new Models.SpeciesPrice
                {
                    SpeciesCode = p.SpeciesCode,
                    Price = p.Price,
                    Currency = p.Currency,
                    MeasuremenyUnit = p.MeasurementUnit,
                    PortCode = p.MarketCode,
                    Timestamp = p.Timestamp.ToDateTime()
                })
                .ToList();

            var res = await _simulationService.UpdatePricesAsync(request.SimulationId, speciesPrices);

            return new UpdateSpeciesPricesResponse() { SimulationId = request.SimulationId };
        }

        public override async Task<GetSalesResponse> GetSales(GetSalesRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("GetSales", request.SimulationId, context);
            _logger.LogInformation($"Ecopath GetSales for {request.SimulationId}...");

            var salesSummaries = await _simulationService.GetSalesSummariesAsync(request.SimulationId, request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime());

            var response = new GetSalesResponse() { SimulationId = request.SimulationId };
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
    }
}
