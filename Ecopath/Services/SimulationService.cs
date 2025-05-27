using Ecopath.EwE;
using Ecopath.Models;

namespace Ecopath.Services
{
    public class SimulationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SimulationService> _logger;
        private readonly Dictionary<string, IEwEController> _controllers = new();

        public SimulationService(IServiceProvider serviceProvider, ILogger<SimulationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<bool> InitAsync(string simulationId, string scenarioId, DateTime startDateTime, string stepSize)
        {
            if (_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} already exists.");
            }

            // Get a new instance from DI
            _controllers[simulationId] = _serviceProvider.GetRequiredService<IEwEController>();

            // ToDo: get the correct configuration for a given scenario
            var res = await _controllers[simulationId].StartAsync(new EwEConfiguration());
            _logger.LogInformation($"Simulation with ID {simulationId} added.");
            return true;
        }

        public async Task<bool> UpdatePricesAsync(string simulationId, List<SpeciesPrice> speciesPrices)
        {
            if (!_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} does not exist.");
            }

            var res = await _controllers[simulationId].UpdatePricesAsync(speciesPrices);
            _logger.LogInformation($"Simulation with ID {simulationId} added.");
            return true;
        }

        public async Task<bool> ContinueAsync(string simulationId)
        {
            if (!_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} does not exist.");
            }

            return await _controllers[simulationId].ContinueAsync();
        }

        public async Task<Biomass> GetBiomassAsync(string simulationId)
        {
            if (!_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} does not exist.");
            }

            return await _controllers[simulationId].GetBiomassAsync();
        }

        public async Task<List<SalesSummary>> GetSalesSummariesAsync(string simulationId, DateTime start, DateTime end)
        {
            if (!_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} does not exist.");
            }

            return await _controllers[simulationId].GetSalesSummariesAsync(start, end);
        }

        public async Task<CatchDispositionSummary> GetCatchDispositionSummaryAsync(string simulationId, DateTime start, DateTime end)
        {
            if (!_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} does not exist.");
            }

            return await _controllers[simulationId].GetCatchDispositionSummaryAsync(start, end);
        }

        public async Task<bool> UpdateCatchDispositionSummary(string simulationId, CatchDispositionSummary catchDispositionSummary)
        {
            if (!_controllers.ContainsKey(simulationId))
            {
                throw new Exception($"Simulation with ID {simulationId} does not exist.");
            }

            var res = await _controllers[simulationId].UpdateCatchDispositionSummaryAsync(catchDispositionSummary);
            return true;
        }
    }
}
