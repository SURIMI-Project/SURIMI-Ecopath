using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathMarketService : MarketService.MarketServiceBase
    {
        private readonly ILogger<EcopathEcologyService> _logger;
        private readonly CheckSimulationService _checkSimulationService;
        private readonly IEwEController _ewEController;

        public EcopathMarketService(ILogger<EcopathEcologyService> logger, CheckSimulationService checkSimulationService, IEwEController ewEController)
        {
            _logger = logger;
            _checkSimulationService = checkSimulationService;
            _ewEController = ewEController;
        }

        public override async Task<UpdateSpeciesPricesResponse> UpdateSpeciesPrices(UpdateSpeciesPricesRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("UpdateSpeciesPrices", request.SimulationId, context);
            _logger.LogInformation($"Updating prices for {request.Prices.Count} species...");

            var speciesPrices = request.Prices
                .Select(p => new Models.SpeciesPrice
                {
                    SpeciesCode = p.Species.SpeciesCode,
                    Price = p.Price,
                    Currency = p.Currency,
                    MeasuremenyUnit = p.MeasurementUnit,
                    PortCode = p.MarketCode,
                    Timestamp = p.Timestamp.ToDateTime()
                })
                .ToList();

            var res = await _ewEController.UpdatePricesAsync(speciesPrices);

            return new UpdateSpeciesPricesResponse() { SimulationId = request.SimulationId };
        }

        public override async Task<GetSalesResponse> GetSales(GetSalesRequest request, ServerCallContext context)
        {
            await _checkSimulationService.CheckIfCorrectSimulationAsync("GetSales", request.SimulationId, context);
            _logger.LogInformation($"Ecopath GetSales for {request.SimulationId}...");

            var salesSummaries = await _ewEController.GetSalesSummariesAsync(request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime());

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
                            Species = new Species
                            {
                                // Note that the market no longer distinguishes species sizes, ages and lengths. This is by design
                                SpeciesCode = sale.SpeciesCode
                            },
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
