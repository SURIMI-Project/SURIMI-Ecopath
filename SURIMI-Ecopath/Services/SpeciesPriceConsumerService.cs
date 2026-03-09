using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services
{
    public class SpeciesPriceConsumerService : Grpc.Surimi.SpeciesPriceConsumerService.SpeciesPriceConsumerServiceBase
    {
        private readonly ILogger<SpeciesPriceConsumerService> m_logger;
        private readonly CheckSimulationService m_checksimulationservice;
        private readonly IEwEController m_controller;

        public SpeciesPriceConsumerService(ILogger<SpeciesPriceConsumerService> logger, CheckSimulationService service, IEwEController controller)
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
                .Select(p => new SURIMI.Datamodel.SpeciesPrice
                {
                    // Note that the market does not distinguish species sizes, ages and lengths, and ignores gear specifics other than gearcode.
                    // Although this is by design but may have to be revisited; the limitations seem like an oversight.
                    SpeciesCode = p.Species.SpeciesCode,
                    GearCode = p.GearCode,
                    Price = p.Price,
                    Currency = p.Currency,
                    MarketCode = p.MarketCode,
                    Timestamp = p.Timestamp.ToDateTime()
                })
                .ToList();

            var res = await m_controller.UpdatePricesAsync(speciesPrices);

            return new UpdateSpeciesPricesResponse() { SimulationId = request.SimulationId };
        }
    }
}
