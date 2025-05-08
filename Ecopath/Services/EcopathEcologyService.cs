using Ecopath.EwE;
using EwECore;
using Grpc.Core;
using Grpc.Surimi;

namespace Ecopath.Services;

public class EcopathEcologyService : EcologyService.EcologyServiceBase
{
    private readonly ILogger<EcopathEcologyService> _logger;
    private readonly EwEController _MEMcontroller;

    public EcopathEcologyService(ILogger<EcopathEcologyService> logger, EwEController mEMcontroller)
    {
        _logger = logger;
        _MEMcontroller = mEMcontroller;
    }

    public override Task<GetBiomassResponse> GetBiomass(GetBiomassRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);
        _logger.LogInformation($"Ecopath Getting biomass for simulation {request.SimulationId}...");
        // Simulate some processing delay
        //Task.Delay(1000).Wait();

        var response = new GetBiomassResponse() { 
            MeasurementUnit = "kg"
        };
        response.BiomassGrids.Add(new BiomassGrid()
        {
            SpeciesId = "PIL"
        });
        response.BiomassGrids[0].BiomassCells.Add(new BiomassCell()
        {
            Longitude = 1.6877561f,
            Latitude = 40.901618f,
            Biomass = 1000.0f
        });
        response.BiomassGrids.Add(new BiomassGrid()
        {
            SpeciesId = "BOG"
        });
        response.BiomassGrids[1].BiomassCells.Add(new BiomassCell()
        {
            Longitude = 1.6170411f,
            Latitude = 40.801618f,
            Biomass = 2500.0f
        });
        return Task.FromResult(response);
    }
}