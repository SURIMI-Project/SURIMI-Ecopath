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

    public override async Task<UpdateCatchDispositionSummaryResponse> UpdateCatchDispositionSummary(UpdateCatchDispositionSummaryRequest request, ServerCallContext context)
    {
        await _checkSimulationService.CheckIfCorrectSimulationAsync("UpdateCatchDispositionSummary", request.SimulationId, context);
        GrpcValidation.ArgumentNotNullOrEmpty(request.MeasurementUnit);

        var catchDispositionSummary = new Ecopath.Models.CatchDispositionSummary
        {
            MeasurementUnit = request.MeasurementUnit,
            DispositionGrids = request.DispositionGrids
            .Select(grpcGrid => new Ecopath.Models.DispositionGrid
            {
                GearCode = grpcGrid.GearCode,
                SpeciesCode = grpcGrid.SpeciesCode,
                DispositionCells = grpcGrid.DispositionCells
                    .Select(grpcCell => new Models.DispositionCell
                    {
                        GrossCatchBiomass = grpcCell.GrossCatch,
                        LiveDiscardsBiomass = grpcCell.LiveDiscards,
                        DeadDiscardsBiomass = grpcCell.DeadDiscards,
                        Latitude = grpcCell.Latitude,
                        Longitude = grpcCell.Longitude
                    })
                    .ToList()
            })
            .ToList()
        };

        var res = await _simulationService.UpdateCatchDispositionSummary(request.SimulationId, catchDispositionSummary);

        return new UpdateCatchDispositionSummaryResponse();
    }
}