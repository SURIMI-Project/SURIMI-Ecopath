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
    [Fact]
    public async Task WriteInitialFilesAsync_UploadsBiomassAndCatchTotals()
    {
        const string environmentVariable = "CREATE_HCR_INITIALISE_FILES";
        string? previousValue = Environment.GetEnvironmentVariable(environmentVariable);
        Environment.SetEnvironmentVariable(environmentVariable, "1");

        try
        {
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

            for (int timestep = 1; timestep <= 12; timestep++)
            {
                catchCell.GrossCatchBiomass = timestep;
                service.CaptureCatchTimestep(catchSummary);
            }

            var biomass = new Biomass { MeasurementUnit = "kg" };
            var biomassGrid = new BiomassGrid { Species = new Species { SpeciesCode = "SPC" } };
            biomassGrid.BiomassCells.Add(new BiomassCell { Biomass = 30 });
            biomass.BiomassGrids.Add(biomassGrid);

            await service.WriteInitialFilesAsync(biomass, CancellationToken.None);

            uploadedFiles.Should().ContainKey("initial_biomasses.csv");
            uploadedFiles["initial_biomasses.csv"].Should().Contain("SPC,,,,30");
            uploadedFiles.Should().ContainKey("initial_catch.csv");
            uploadedFiles["initial_catch.csv"].Should().Contain("Catch").And.Contain("SPC,,,,78");
            uploadedFiles["initial_catch.csv"].Should().NotContain("CatchKg");
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariable, previousValue);
        }
    }
}
