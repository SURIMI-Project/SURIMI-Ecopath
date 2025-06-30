using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathEcologyService : EcologyService.EcologyServiceBase
{
    private readonly ILogger<EcopathEcologyService> _logger;
    private readonly CheckSimulationService _checkSimulationService;
    private readonly IEwEController _ewEController;

    public EcopathEcologyService(ILogger<EcopathEcologyService> logger, CheckSimulationService checkSimulationService, IEwEController ewEController)
    {
        _logger = logger;
        _checkSimulationService = checkSimulationService;
        _ewEController = ewEController;
    }

    public override async Task<GetBiomassResponse> GetBiomass(GetBiomassRequest request, ServerCallContext context)
    {
        await _checkSimulationService.CheckIfCorrectSimulationAsync("GetBiomass", request.SimulationId, context);
        _logger.LogInformation($"GetBiomass for simulation {request.SimulationId}");

        var biomass = await _ewEController.GetBiomassAsync();

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