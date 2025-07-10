using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class EcopathMarketService : MarketService.MarketServiceBase
    {
        private readonly ILogger<EcopathEcologyService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_controller;

        public EcopathMarketService(ILogger<EcopathEcologyService> logger, CheckSimulationService service, IEwEController controller)
        {
            m_logger = logger;
            m_checksimulationservice = service;
            m_controller = controller;
        }

        public override async Task<UpdateSpeciesPricesResponse> UpdateSpeciesPrices(UpdateSpeciesPricesRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("UpdateSpeciesPrices", request.SimulationId, context);
            m_logger.LogInformation($"Updating prices for {request.Prices.Count} species...");

            var speciesPrices = request.Prices
                .Select(p => new Models.SpeciesPrice
                {
                    // Note that the market does not distinguish species sizes, ages and lengths. This is by design but may have to be revisited
                    // It feels as an oversight not at least facilitating this detail
                    SpeciesCode = p.Species.SpeciesCode,
                    Price = p.Price,
                    Currency = p.Currency,
                    MeasuremenyUnit = p.MeasurementUnit,
                    MarketCode = p.MarketCode,
                    Timestamp = p.Timestamp.ToDateTime()
                })
                .ToList();

            var res = await m_controller.UpdatePricesAsync(speciesPrices);

            return new UpdateSpeciesPricesResponse() { SimulationId = request.SimulationId };
        }

        public override async Task<GetSalesResponse> GetSales(GetSalesRequest request, ServerCallContext context)
        {
            await m_checksimulationservice.CheckIfCorrectSimulationAsync("GetSales", request.SimulationId, context);
            m_logger.LogInformation($"Ecopath GetSales for {request.SimulationId}...");

            var salesSummaries = await m_controller.GetSalesSummariesAsync(request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime());

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
                                // Note that the market does not distinguish species sizes, ages and lengths. This is by design but may have to be revisited
                                // It feels as an oversight not at least facilitating this detail
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
