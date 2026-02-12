using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathEcologyService : EcologyService.EcologyServiceBase
{
    private readonly ILogger<EcopathEcologyService> m_logger;
    private readonly CheckSimulationService m_checksimulationservice;
    private readonly IEwEController m_ewecontroller;

    public EcopathEcologyService(ILogger<EcopathEcologyService> logger, CheckSimulationService service, IEwEController controller)
    {
        m_logger = logger;
        m_checksimulationservice = service;
        m_ewecontroller = controller;
    }

    public override async Task<GetBiomassResponse> GetBiomass(GetBiomassRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync("GetBiomass", request.SimulationId, context);
        m_logger.LogInformation($"GetBiomass for simulation {request.SimulationId}");

        var biomass = await m_ewecontroller.GetBiomassAsync();

        var grpcBiomass = new GetBiomassResponse
        {
            BiomassSummary = new BiomassSummary(),
            SimulationId = request.SimulationId,
            DateTime = request.DateTime
        };

        if (biomass.BiomassGrids != null)
        {
            grpcBiomass.BiomassSummary.BiomassGrids.AddRange(
                biomass.BiomassGrids.Select(grid => new Grpc.Surimi.BiomassGrid
                {
                    Species = new Species
                    {
                        SpeciesCode = grid.Species.SpeciesCode ?? string.Empty,
                        LengthClass = grid.Species.LengthClass ?? string.Empty,
                        Age = grid.Species.Age ?? string.Empty,
                        LifeStage = grid.Species.LifeStage ?? string.Empty
                    },
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