using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathEcologyService : EcologyService.EcologyServiceBase
{
    private readonly ILogger<EcopathEcologyService> _logger;
    private readonly SimulationService _simulationService;

    public EcopathEcologyService(ILogger<EcopathEcologyService> logger, SimulationService simulationService)
    {
        _logger = logger;
        _simulationService = simulationService;
    }

    public override async Task<GetBiomassResponse> GetBiomass(GetBiomassRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
        _logger.LogInformation($"Ecopath Getting biomass for simulation {request.SimulationId}...");

        var biomass = await _simulationService.GetBiomassAsync(request.SimulationId);

        var grpcBiomass = new GetBiomassResponse
        {
            MeasurementUnit = biomass.MeasurementUnit ?? string.Empty
        };

        if (biomass.BiomassGrids != null)
        {
            grpcBiomass.BiomassGrids.AddRange(
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