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
    public async Task WriteInitialFilesAsync_UploadsBiomassAndCatchTotals()
    {
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

            var biomass = new Biomass { MeasurementUnit = "kg" };
            var biomassGrid = new BiomassGrid { Species = new Species { SpeciesCode = "SPC" } };
            biomassGrid.BiomassCells.Add(new BiomassCell { Biomass = 30 });
            biomass.BiomassGrids.Add(biomassGrid);

            // Act
            service.BeginInitialization();
            for (int timestep = 1; timestep <= 12; timestep++)
            {
                catchCell.GrossCatchBiomass = timestep;
                service.CaptureCatchTimestep(catchSummary);
            }
            await service.WriteInitialFilesAsync(biomass, "scenario", CancellationToken.None);

            // Assert
            uploadedFiles.Should().ContainKey("scenario/initial_biomasses.csv");
            uploadedFiles["scenario/initial_biomasses.csv"].Should().Contain("SPC,,,,30");
            uploadedFiles.Should().ContainKey("scenario/initial_catch.csv");
            uploadedFiles["scenario/initial_catch.csv"].Should().Contain("Catch").And.Contain("SPC,,,,78");
            uploadedFiles["scenario/initial_catch.csv"].Should().NotContain("CatchKg");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }
}
