using Ecopath.EwE;
using Grpc.Core;
using Grpc.Surimi;
using SURIMI.Common.gRPC;
using SURIMI.Common.gRPC.Services;

namespace Ecopath.Services;

public class EcologyService : Grpc.Surimi.EcologyService.EcologyServiceBase
{
    private readonly ILogger<EcologyService> m_logger;
    private readonly ICheckSimulationService m_checksimulationservice;
    private readonly IEwEController m_controller;
    private readonly string _version;

    public EcologyService(ILogger<EcologyService> logger, ICheckSimulationService service, IEwEController controller, ProtocolVersionService protocolVersionService)
    {
        m_logger = logger;
        m_checksimulationservice = service;
        m_controller = controller;
        _version = protocolVersionService.LoadVersion();
    }

    public override async Task<InitialiseSimulationResponse> InitialiseSimulation(InitialiseSimulationRequest request, ServerCallContext context)
    {
        GrpcValidation.ArgumentNotNullOrEmpty(request.ScenarioName, "ScenarioName");
        await m_checksimulationservice.ReserveSimulationAsync(request.SimulationId, context);


        m_logger.LogInformation("Initializing simulation {SimulationId}, scenario {ScenarioName}, climate scenario {ClimateScenario}...", request.SimulationId, request.ScenarioName, request.ClimateScenario);
        try
        {
            var contract = GetSurimiContract(request.Simulation);

            var result = await m_controller.StartAsync(contract, request.ScenarioName, request.ClimateScenario);
            if (result != 1)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to initialise Ecopath"));
            }
            return new InitialiseSimulationResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            m_checksimulationservice.ReleaseSimulation(request.SimulationId);
            m_logger.LogError(ex, "In Initialise. EwE - exception ...{Message}", ex.Message);
            throw;
        }
    }

    public override async Task<FinaliseSimulationResponse> FinaliseSimulation(FinaliseSimulationRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_checksimulationservice.ReleaseSimulation(request.SimulationId);
        m_logger.LogInformation("Finalizing simulation {SimulationId}", request.SimulationId);

        try
        {
            var result = await m_controller.StopAsync();
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to finalise Ecopath"));
            }
            return new FinaliseSimulationResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            m_logger.LogError(ex, "In Finalise - exception");
            throw;
        }
    }

    public override async Task<CancelSimulationResponse> CancelSimulation(CancelSimulationRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_checksimulationservice.ReleaseSimulation(request.SimulationId);
        m_logger.LogInformation("Cancel simulation {SimulationId}", request.SimulationId);

        try
        {
            var result = await m_controller.StopAsync();
            if (result == false)
            {
                throw new RpcException(new Status(StatusCode.Internal, "Failed to cancel Ecopath"));
            }
            return new CancelSimulationResponse() { SimulationId = request.SimulationId };
        }
        catch (Exception ex)
        {
            m_logger.LogError(ex, "In Cancel - exception");
            throw;
        }
    }

    public override async Task<SimulateStepResponse> SimulateStep(SimulateStepRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("Simulate step for simulation {SimulationId}", request.SimulationId);

        var res = await m_controller.ContinueAsync();

        return new SimulateStepResponse() { SimulationId = request.SimulationId };
    }

    public override async Task<GetBiomassResponse> GetBiomass(GetBiomassRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("GetBiomass for simulation {SimulationId}", request.SimulationId);

        var biomass = await m_controller.GetBiomassAsync();

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

    public override async Task<UpdateCatchDispositionResponse> UpdateCatchDisposition(UpdateCatchDispositionRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        GrpcValidation.ArgumentNotNullOrEmpty(request.SimulationId);

        var catchDisposition = new SURIMI.Datamodel.CatchDispositionSummary
        {
            DispositionGrids = request.CatchDispositionSummary.DispositionGrids
            .Select(grid => new SURIMI.Datamodel.DispositionGrid
            {
                FleetSegment = new SURIMI.Datamodel.FleetSegment
                {
                    GearCode = grid.FleetSegment.GearCode,
                    CountryCode = grid.FleetSegment.CountryCode ?? string.Empty
                },
                Species = new SURIMI.Datamodel.Species
                {
                    SpeciesCode = grid.Species.SpeciesCode,
                    LengthClass = grid.Species.LengthClass ?? string.Empty,
                    Age = grid.Species.Age ?? string.Empty,
                    LifeStage = grid.Species.LifeStage ?? string.Empty
                },
                DispositionCells = grid.DispositionCells
                    .Select(grpcCell => new SURIMI.Datamodel.DispositionCell
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

        var res = await m_controller.UpdateCatchDispositionSummaryAsync(catchDisposition);

        return new UpdateCatchDispositionResponse() { SimulationId = request.SimulationId };
    }

    public override async Task<GetCatchDispositionResponse> GetCatchDisposition(GetCatchDispositionRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("Ecopath GetCatchDisposition for {SimulationId}...", request.SimulationId);

        var catchDisposition = await m_controller.GetCatchDispositionSummaryAsync(
            request.StartDateTime.ToDateTime(),
            request.EndDateTime.ToDateTime()
        );

        var response = new GetCatchDispositionResponse
        {
            CatchDispositionSummary = new CatchDispositionSummary(),
            SimulationId = request.SimulationId,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime
        };

        if (catchDisposition?.DispositionGrids != null)
        {
            response.CatchDispositionSummary.DispositionGrids.AddRange(
                catchDisposition.DispositionGrids.Select(grid =>
                {
                    var dispositionGrid = new DispositionGrid
                    {
                        Species = new Species
                        {
                            SpeciesCode = grid.Species.SpeciesCode ?? string.Empty,
                            LengthClass = grid.Species.LengthClass ?? string.Empty,
                            Age = grid.Species.Age ?? string.Empty,
                            LifeStage = grid.Species.LifeStage ?? string.Empty
                        },
                        FleetSegment = new FleetSegment
                        {
                            GearCode = grid.FleetSegment.GearCode ?? string.Empty,
                            CountryCode = grid.FleetSegment.CountryCode ?? string.Empty
                        }
                    };
                    if (grid.DispositionCells != null)
                    {
                        dispositionGrid.DispositionCells.AddRange(
                            grid.DispositionCells.Select(cell => new DispositionCell
                            {
                                GrossCatch = cell.GrossCatchBiomass,
                                LiveDiscards = cell.LiveDiscardsBiomass,
                                DeadDiscards = cell.DeadDiscardsBiomass,
                                Latitude = cell.Latitude,
                                Longitude = cell.Longitude
                            }).Where(cell => cell.GrossCatch != 0 || cell.LiveDiscards != 0 || cell.DeadDiscards != 0) // Filter out empty cells, as they are not useful and only add noise to the response
                        );
                    }
                    return dispositionGrid;
                })
            );
        }

        return response;
    }

    public override async Task<GetFishingActivityResponse> GetFishingActivity(GetFishingActivityRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("Getting Fishing Activity for Simulation {SimulationId}...", request.SimulationId);

        var fishingActivity = await m_controller.GetFishingActivityAsync();

        var fishingActivityGrpc = new FishingActivitySummary
        {
            FishingActivities = { fishingActivity.FishingActivities.Select(activity => new FishingActivity
                {
                    FleetSegment = new FleetSegment
                    {
                        GearCode = activity.FleetSegment?.GearCode ?? string.Empty,
                        CountryCode = activity.FleetSegment?.CountryCode ?? string.Empty
                    },
                    FishingActivityRatio = activity.FishingActivityRatio
                })}
        };

        var response = new GetFishingActivityResponse
        {
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            SimulationId = request.SimulationId,
            FishingActivitySummary = fishingActivityGrpc
        };

        return response;
    }

    public override async Task<UpdateRegulationsResponse> UpdateRegulations(UpdateRegulationsRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("Updating Regulations for Simulation {SimulationId}", request.SimulationId);

        var regulations = new SURIMI.Datamodel.RegulationsSummary
        {
            TotalAllowableCatches = request.RegulationsSummary.TotalAllowableCatches
                .Select(tac => new SURIMI.Datamodel.TotalAllowableCatch
                {
                    FleetSegment = new SURIMI.Datamodel.FleetSegment
                    {
                        GearCode = tac.FleetSegment.GearCode,
                        CountryCode = tac.FleetSegment.CountryCode ?? string.Empty
                    },
                    Species = new SURIMI.Datamodel.Species
                    {
                        SpeciesCode = tac.Species.SpeciesCode,
                        LengthClass = tac.Species.LengthClass ?? string.Empty,
                        Age = tac.Species.Age ?? string.Empty,
                        LifeStage = tac.Species.LifeStage ?? string.Empty
                    },
                })
                .ToList()
        };

        var res = await m_controller.UpdateRegulationsAsync(regulations);

        return new UpdateRegulationsResponse() { SimulationId = request.SimulationId };
    }

    public override async Task<GetSalesResponse> GetSales(GetSalesRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("Ecopath GetSales for {SimulationId}...", request.SimulationId);

        var salesSummaries = await m_controller.GetSalesSummariesAsync(request.StartDateTime.ToDateTime(), request.EndDateTime.ToDateTime());

        var response = new GetSalesResponse() { SimulationId = request.SimulationId, StartDateTime = request.StartDateTime, EndDateTime = request.EndDateTime, SalesSummary = new SalesSummary() };
        response.SalesSummary.MarketSales.AddRange(
            salesSummaries.Select(summary =>
            {
                var grpcSummary = new MarketSales
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
                            CountryCode = summary.MarketCode == "ES" ? "ESP" : "FRA"    // TODO This is a hack to get the country code for the fleet segment. 
                        },
                        Quantity = sale.Quantity,
                        Value = sale.Value,
                        CategoryCode = "Fresh - Whole"  // TODO. THis is true for the northwestern_med scenario
                    }));
                }

                return grpcSummary;
            }).Where(m => m.Sales.Count != 0)   // Filter out empty summaries, as they are not useful and only add noise to the response
        );

        return response;
    }

    public override async Task<UpdateSpeciesPricesResponse> UpdateSpeciesPrices(UpdateSpeciesPricesRequest request, ServerCallContext context)
    {
        await m_checksimulationservice.CheckIfCorrectSimulationAsync(request.SimulationId, context);
        m_logger.LogInformation("Updating prices for {SpeciesCount} species...", request.SpeciesPriceSummary.SpeciesPrices.Count);

        var speciesPrices = request.SpeciesPriceSummary.SpeciesPrices
            .Select(p => new SURIMI.Datamodel.SpeciesPrice
            {
                // Note that the market does not distinguish species sizes, ages and lengths, and ignores gear specifics other than gearcode.
                // Although this is by design but may have to be revisited; the limitations seem like an oversight.
                SpeciesCode = p.Species.SpeciesCode,
                CategoryCode = p.CategoryCode,
                Price = p.Price,
                Currency = p.Currency,
                MarketCode = p.MarketCode,
                Timestamp = request.DateTime.ToDateTime()
            })
            .ToList();

        var res = await m_controller.UpdatePricesAsync(speciesPrices);

        return new UpdateSpeciesPricesResponse() { SimulationId = request.SimulationId };
    }

    public override Task<GetProtocolVersionResponse> GetProtocolVersion(GetProtocolVersionRequest request, ServerCallContext context)
    {
        return Task.FromResult(new GetProtocolVersionResponse() { ProtocolVersion = _version });
    }

    /// <summary>
    /// Mapping method from gRPC Surimi Simulation to SURIMI Datamodel SurimiContract.
    /// </summary>
    /// <param name="simulation"></param>
    /// <returns></returns>
    private SURIMI.Datamodel.SurimiContract GetSurimiContract(Grpc.Surimi.Simulation simulation)
    {
        return new SURIMI.Datamodel.SurimiContract
        {
            Simulation = new SURIMI.Datamodel.Simulation()
            {
                CaseStudyName = simulation.CaseStudyName,
                StartDateTime = simulation.StartDateTime.ToDateTime(),
                MaximumEndDateTime = simulation.MaximumEndDateTime.ToDateTime(),
                TimeStep = simulation.TimeStep,
                Geography = new SURIMI.Datamodel.Geography()
                {
                    Crs = new SURIMI.Datamodel.CoordinateReferenceSystem()
                    {
                        Authority = simulation.Geography.Crs.Authority,
                        Code = simulation.Geography.Crs.Code,
                        Name = simulation.Geography.Crs.Name
                    },
                    RasterCellOrigin = Enum.Parse<SURIMI.Datamodel.RasterCellOrigin>(simulation.Geography.RasterCellOrigin.ToString()),
                    Xres = simulation.Geography.Xres,
                    Yres = simulation.Geography.Yres,
                    Ncol = simulation.Geography.Ncol,
                    Nrow = simulation.Geography.Nrow,
                    Xmin = simulation.Geography.Xmin,
                    Xmax = simulation.Geography.Xmax,
                    Ymin = simulation.Geography.Ymin,
                    Ymax = simulation.Geography.Ymax
                }
            },
            Standards = new SURIMI.Datamodel.Standards()
            {
                Currency = simulation.Standards.Currency,
                CountryCode = simulation.Standards.CountryCode,
                DateAndTime = simulation.Standards.DateAndTime,
                GearCode = simulation.Standards.GearCode,
                LifeStage = simulation.Standards.LifeStage,
                MarketCode = simulation.Standards.MarketCode,
                SpeciesCode = simulation.Standards.SpeciesCode,
                Measurements = new SURIMI.Datamodel.Measurement()
                {
                    System = simulation.Standards.Measurements.System,
                    Units = simulation.Standards.Measurements.Units
                        .Select(u => new SURIMI.Datamodel.UnitType
                        {
                            Quantity = u.Quantity ?? string.Empty,
                            Unit = u.Unit_ ?? string.Empty, // Unit_ because 'unit' may be reserved in proto
                        })
                        .ToList()
                }
            },
            Items = new SURIMI.Datamodel.Items()
            {
                Species = simulation.Items.Species
                    .Select(s => new SURIMI.Datamodel.Species
                    {
                        SpeciesCode = s.SpeciesCode,
                        LengthClass = s.LengthClass,
                        Age = s.Age,
                        LifeStage = s.LifeStage
                    })
                    .ToList(),
                FleetSegments = simulation.Items.FleetSegments
                    .Select(f => new SURIMI.Datamodel.FleetSegment
                    {
                        GearCode = f.GearCode,
                        VesselLengthClass = f.VesselLengthClass,
                        Scale = f.Scale,
                        CountryCode = f.CountryCode,
                    })
                    .ToList(),
                Currencies = simulation.Items.Currencies
                    .Select(c => new SURIMI.Datamodel.Currency
                    {
                        CurrencyCode = c.Code
                    })
                    .ToList(),
                Markets = simulation.Items.Markets
                    .Select(c => new SURIMI.Datamodel.Market
                    {
                        MarketCode = c.MarketCode,
                    })
                    .ToList(),
                PriceCategories = simulation.Items.PriceCategories
                    .Select(c => new SURIMI.Datamodel.PriceCategory
                    {
                        CategoryCode = c.CategoryCode,
                    })
                    .ToList(),
                ClimateScenarios = simulation.Items.ClimateScenarios
                    .Select(c => new SURIMI.Datamodel.ClimateScenario
                    {
                        ClimateScenarioCode = c.ClimateScenarioCode,
                    })
                    .ToList()
            }
        };
    }
}