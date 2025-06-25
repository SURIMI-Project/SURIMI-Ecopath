using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathEcologyService : EcologyService.EcologyServiceBase
{
    private readonly ILogger<EcopathEcologyService> _logger;
    private readonly SimulationService _simulationService;
    private readonly CheckSimulationService _checkSimulationService;

    public EcopathEcologyService(ILogger<EcopathEcologyService> logger, SimulationService simulationService, CheckSimulationService checkSimulationService)
    {
        _logger = logger;
        _simulationService = simulationService;
        _checkSimulationService = checkSimulationService;
    }

    public override async Task<GetBiomassResponse> GetBiomass(GetBiomassRequest request, ServerCallContext context)
    {
        await _checkSimulationService.CheckIfCorrectSimulationAsync("GetBiomass", request.SimulationId, context);

        var biomass = await _simulationService.GetBiomassAsync(request.SimulationId);

        var grpcBiomass = new GetBiomassResponse
        {
            BiomassSummary = new BiomassSummary
            {
                MeasurementUnit = biomass.MeasurementUnit ?? string.Empty
            },
            SimulationId = request.SimulationId
        };

        if (biomass.BiomassGrids != null)
        {
            grpcBiomass.BiomassSummary.BiomassGrids.AddRange(
                biomass.BiomassGrids.Select(grid => new Grpc.Surimi.BiomassGrid
                {
                    SpeciesCode = grid.SpeciesCode ?? string.Empty,
                    BiomassCells = { grid.BiomassCells?.Select(cell => new Grpc.Surimi.BiomassCell
                    {
                        Biomass = cell.Biomass,
                        Latitude = cell.Latitude,
                        Longitude = cell.Longitude
                    }) ?? Enumerable.Empty<Grpc.Surimi.BiomassCell>() }
                })
            );
        }

        return grpcBiomass;
    }
}