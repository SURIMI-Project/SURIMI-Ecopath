using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class SalesProviderService : Grpc.Surimi.SalesProviderService.SalesProviderServiceBase
    {
        private readonly ILogger<EcologyProviderService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_controller;

        public SalesProviderService(ILogger<EcologyProviderService> logger, CheckSimulationService service, IEwEController controller)
        {
            m_logger = logger;
            m_checksimulationservice = service;
            m_controller = controller;
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
                        MarketCode = summary.MarketCode,
                        Currency = summary.Currency,
                    };

                    if (summary.Sales != null)
                    {
                        grpcSummary.Sales.AddRange(summary.Sales.Select(sale => new Sale
                        {
                            // WHY DO WE DEFINE NEAR EMPTY OBJECTS HERE, WHILE ONLY CODES IN UpdateSpeciesPrices? THIS SHOULD FOLLW THE SAME LOGIC
                            Species = new Species
                            {
                                // Note that the market does not distinguish species sizes, ages and lengths, and ignores gear specifics other than gearcode.
                                // Although this is by design but may have to be revisited; the limitations seem like an oversight.
                                SpeciesCode = sale.SpeciesCode
                            },
                            FleetSegment = new FleetSegment()
                            {
                                GearCode = sale.GearCode,
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
