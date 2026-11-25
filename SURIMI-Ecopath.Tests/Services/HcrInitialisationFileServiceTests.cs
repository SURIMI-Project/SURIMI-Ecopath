using System.Text;
using Ecopath.Services;
using Eii.BlobStore;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SURIMI.Datamodel;
using Xunit;

namespace Ecopath.Tests.Services;

public class HcrInitialisationFileServiceTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void BeginInitialization_SetsEnabledFromBooleanEnvironmentVariable(string? setting, bool expectedEnabled)
    {
        const string environmentVariable = "CREATE_HCR_INITIALISE_FILES";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, setting);

        try
        {
            // Arrange
            var service = new HcrInitialisationFileService(
                Mock.Of<IBlobStore>(),
                Mock.Of<ILogger<HcrInitialisationFileService>>());

            // Act
            service.BeginInitialization();

            // Assert
            service.Enabled.Should().Be(expectedEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }

    [Fact]
    public void BeginInitialization_ThrowsForInvalidBooleanEnvironmentVariable()
    {
        const string environmentVariable = "CREATE_HCR_INITIALISE_FILES";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, "enabled");

        try
        {
            // Arrange
            var service = new HcrInitialisationFileService(
                Mock.Of<IBlobStore>(),
                Mock.Of<ILogger<HcrInitialisationFileService>>());

            // Act
            Action act = service.BeginInitialization;

            // Assert
            act.Should().Throw<FormatException>()
                .WithMessage("Environment variable CREATE_HCR_INITIALISE_FILES must be 'true' or 'false'.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }

    [Fact]
    public async Task WriteInitialFilesAsync_UploadsHcrDataInitialQuotaRecruitmentAndQuotaShares()
    {
        // Arrange
        const string environmentVariable = "CREATE_HCR_INITIALISE_FILES";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, "true");

        try
        {
            // Arrange
            var blobStore = new Mock<IBlobStore>();
            var logger = new Mock<ILogger<HcrInitialisationFileService>>();
            var uploadedFiles = new Dictionary<string, string>();
            blobStore
                .Setup(store => store.UploadAsync(
                    It.IsAny<string>(),
                    PathType.Output,
                    It.IsAny<Stream>(),
                    "text/csv",
                    It.IsAny<IReadOnlyDictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, PathType, Stream, string, IReadOnlyDictionary<string, string>, CancellationToken>(
                    (fileName, _, content, _, _, _) =>
                    {
                        using var reader = new StreamReader(content, Encoding.UTF8, true, 1024, true);
                        uploadedFiles[fileName] = reader.ReadToEnd();
                    })
                .Returns(Task.CompletedTask);

            var service = new HcrInitialisationFileService(blobStore.Object, logger.Object);
            service.BeginInitialization();
            var catchSummary = new CatchDispositionSummary();
            var catchGrid = new DispositionGrid
            {
                FleetSegment = new FleetSegment { GearCode = "GEAR", CountryCode = "USA" },
                Species = new Species { SpeciesCode = "SPC" }
            };
            var catchCell = new DispositionCell
            {
                DeadDiscardsBiomass = 0.25
            };
            catchGrid.DispositionCells.Add(catchCell);
            catchSummary.DispositionGrids.Add(catchGrid);
            var additionalCatchGrid = new DispositionGrid
            {
                FleetSegment = new FleetSegment { GearCode = "GEAR", CountryCode = "USA" },
                Species = new Species { SpeciesCode = "SPC", Age = "adult" }
            };
            var additionalCatchCell = new DispositionCell { GrossCatchBiomass = 0.5 };
            additionalCatchGrid.DispositionCells.Add(additionalCatchCell);
            catchSummary.DispositionGrids.Add(additionalCatchGrid);

            var biomass = new Biomass { MeasurementUnit = "kg" };
            var biomassGrid = new BiomassGrid { Species = new Species { SpeciesCode = "SPC" } };
            biomassGrid.BiomassCells.Add(new BiomassCell { Biomass = 30 });
            biomass.BiomassGrids.Add(biomassGrid);
            var additionalBiomassGrid = new BiomassGrid { Species = new Species { SpeciesCode = "SPC", Age = "adult" } };
            additionalBiomassGrid.BiomassCells.Add(new BiomassCell { Biomass = 2 });
            biomass.BiomassGrids.Add(additionalBiomassGrid);
            var uncaughtGrid = new BiomassGrid { Species = new Species { SpeciesCode = "SPC", LifeStage = "juvenile" } };
            uncaughtGrid.BiomassCells.Add(new BiomassCell { Biomass = 5 });
            biomass.BiomassGrids.Add(uncaughtGrid);
            MseRecruitment[] recruitment =
            [
                new("HKE", "juvenile", 0.7677637f, 0.2f, 0.8f),
                new("BOG", "", 0.893f, 0.2f, 0.8f),
                new("HKE", "adult", 0.7134952f, 0.2f, 0.8f),
                new("hke", "ADULT", 0.7134952f, 0.2f, 0.8f)
            ];
            var adultShares = new Dictionary<(string, string), double> { [("OTB", "FRA")] = 0.25, [("OTB", "ESP")] = 0.75 };
            MseQuotaShare[] quotaShares =
            [
                new("HKE", "JUVENILE", new Dictionary<(string, string), double> { [("OTB", "FRA")] = 1 }),
                new("HKE", "ADULT", adultShares),
                new("HKE", "ADULT", adultShares),
                new("BOG", "", new Dictionary<(string, string), double> { [("OTB", "ESP")] = 0.75 }),
                new("PIL", "ADULT", new Dictionary<(string, string), double> { [("OTB", "ESP")] = 1 }),
                new("PIL", "ADULT", new Dictionary<(string, string), double> { [("OTB", "FRA")] = 1 })
            ];

            // Act
            await service.WriteRecruitmentFileAsync(recruitment, "scenario", CancellationToken.None);
            await service.WriteQuotaShareFileAsync(quotaShares, "scenario", CancellationToken.None);
            for (int timestep = 1; timestep <= 12; timestep++)
            {
                catchCell.GrossCatchBiomass = timestep;
                service.CaptureCatchTimestep(catchSummary);
            }
            DefaultHarvestControlRules[] defaultHarvestControlRules = [new("SPC", "", "", "", 12.5, 40, 0.25)];
            await service.WriteInitialFilesAsync(biomass, defaultHarvestControlRules, "scenario", CancellationToken.None);

            // Assert
            uploadedFiles.Should().ContainKey("scenario/scenario_initial_hrc_data.csv");
            uploadedFiles["scenario/scenario_initial_hrc_data.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().Equal(
                "SpeciesCode,LengthClass,Age,LifeStage,Biomass,Catch,BiomassLimit,BiomassBase,FMax",
                "SPC,,,,30,78,12.5,40,0.25",
                "SPC,,,juvenile,5,0,0,0,0",
                "SPC,,adult,,2,6,0,0,0");
            uploadedFiles.Should().ContainKey("scenario/scenario_initial_quota.csv");
            uploadedFiles["scenario/scenario_initial_quota.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().Equal(
                "species_code,life_stage,TAC",
                "SPC,,84",
                "SPC,juvenile,0");
            uploadedFiles.Should().HaveCount(4);
            uploadedFiles.Should().ContainKey("scenario/scenario_recruitment.csv");
            uploadedFiles["scenario/scenario_recruitment.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().Equal(
                "species_code,life_stage,RstockRatio,RHalfB0Ratio,cvRec",
                "BOG,,0.893,0.2,0.8",
                "HKE,ADULT,0.7134952,0.2,0.8",
                "HKE,JUVENILE,0.7677637,0.2,0.8");
            uploadedFiles.Should().ContainKey("scenario/scenario_quotashare.csv");
            uploadedFiles["scenario/scenario_quotashare.csv"].Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().Equal(
                "species_code,life_stage,OTB|ESP,OTB|FRA",
                "HKE,ADULT,0.75,0.25",
                "HKE,JUVENILE,0,1");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData(null, false)]
    [InlineData("true", true)]
    public void BeginInitialization_EnablesExportsOnlyWhenOptedIn(string? setting, bool expected)
    {
        // Arrange
        const string environmentVariable = "CREATE_HCR_INITIALISE_FILES";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, setting);
        var service = new HcrInitialisationFileService(Mock.Of<IBlobStore>(), Mock.Of<ILogger<HcrInitialisationFileService>>());

        try
        {
            // Act
            service.BeginInitialization();

            // Assert
            service.Enabled.Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }
}
