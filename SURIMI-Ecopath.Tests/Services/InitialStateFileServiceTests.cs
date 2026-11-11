using System.Globalization;
using System.Text;
using Ecopath.EwE;
using Ecopath.Services;
using Eii.BlobStore;
using FluentAssertions;
using Grpc.Core;
using Grpc.Surimi;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using SURIMI.Common.gRPC.Services;
using Xunit;
using EcologyService = Ecopath.Services.EcologyService;

namespace Ecopath.Tests.Services;

public class InitialStateFileServiceTests
{
    private const string BiomassHeader = "SpeciesCode,LengthClass,Age,LifeStage,Biomass\n";
    private const string CatchHeader = "SpeciesCode,LengthClass,Age,LifeStage,Catch\n";
    private readonly Mock<IBlobStore> m_blobStore = new(MockBehavior.Strict);

    [Fact]
    public async Task GetInitialBiomasses_ReturnsUnchangedValuesAndCompleteSpeciesKeys()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        SetupFile("custom_scenario", "initial_biomasses.csv", BiomassHeader + "ANE,10,2,adult,123.5\nANE,,,juvenile,20\n", cancellation.Token);
        var context = new Mock<ServerCallContext>();
        context.Protected().SetupGet<CancellationToken>("CancellationTokenCore").Returns(cancellation.Token);
        var service = CreateEcologyService(CreateReader());

        // Act
        var response = await service.GetInitialBiomasses(new GetInitialBiomassesRequest { ScenarioName = "custom_scenario" }, context.Object);

        // Assert
        response.InitialBiomassSummary.InitialBiomasses.Should().HaveCount(2);
        var adult = response.InitialBiomassSummary.InitialBiomasses[0];
        adult.Species.Should().Be(new Species { SpeciesCode = "ANE", LengthClass = "10", Age = "2", LifeStage = "adult" });
        adult.Biomass.Should().Be(123.5);
        response.InitialBiomassSummary.InitialBiomasses[1].Biomass.Should().Be(20);
        m_blobStore.VerifyAll();
    }

    [Fact]
    public async Task GetInitialFishingMortalities_MatchesFullKeysRegardlessOfRowOrder()
    {
        // Arrange
        SetupFile("scenario", "initial_biomasses.csv", BiomassHeader + "ANE,10,2,adult,100\nANE,,,juvenile,20\n");
        SetupFile("scenario", "initial_catch.csv", CatchHeader + "ANE,,,juvenile,10\nANE,10,2,adult,0\n");
        var service = CreateEcologyService(CreateReader());

        // Act
        var response = await service.GetInitialFishingMortalities(new GetInitialFishingMortalitiesRequest { ScenarioName = "scenario" }, Mock.Of<ServerCallContext>());

        // Assert
        response.InitialFishingMortalitySummary.InitialFishingMortalities.Should().HaveCount(2);
        var adult = response.InitialFishingMortalitySummary.InitialFishingMortalities[0];
        adult.Species.Should().Be(new Species { SpeciesCode = "ANE", LengthClass = "10", Age = "2", LifeStage = "adult" });
        adult.FishingMortality.Should().Be(0);
        var juvenile = response.InitialFishingMortalitySummary.InitialFishingMortalities[1];
        juvenile.Species.LifeStage.Should().Be("juvenile");
        juvenile.FishingMortality.Should().Be(0.5);
        m_blobStore.VerifyAll();
    }

    [Fact]
    public async Task ReadBiomassesAsync_UsesInvariantCulture()
    {
        // Arrange
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        SetupFile("scenario", "initial_biomasses.csv", BiomassHeader + "ANE,,,,123.5\n");
        var reader = CreateReader();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
        try
        {
            // Act
            var values = await reader.ReadBiomassesAsync("scenario", CancellationToken.None);

            // Assert
            values.Values.Should().ContainSingle().Which.Should().Be(123.5);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("SpeciesCode,LengthClass,Age,LifeStage,Biomass\nANE,,,,0\n", "SpeciesCode,LengthClass,Age,LifeStage,Catch\nANE,,,,0\n")]
    [InlineData("SpeciesCode,LengthClass,Age,LifeStage,Biomass\nANE,,,,10\n", "SpeciesCode,LengthClass,Age,LifeStage,Catch\nPIL,,,,1\n")]
    [InlineData("SpeciesCode,LengthClass,Age,LifeStage,Biomass\nANE,,,,1E-300\n", "SpeciesCode,LengthClass,Age,LifeStage,Catch\nANE,,,,1E300\n")]
    public async Task ReadFishingMortalitiesAsync_RejectsUndefinedOrMismatchedData(string biomass, string caught)
    {
        // Arrange
        SetupFile("scenario", "initial_biomasses.csv", biomass);
        SetupFile("scenario", "initial_catch.csv", caught);
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadFishingMortalitiesAsync("scenario", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("SpeciesCode,Biomass\nANE,10\n")]
    [InlineData(BiomassHeader)]
    [InlineData(BiomassHeader + "ANE,,,,10\nANE,,,,20\n")]
    [InlineData(BiomassHeader + "ANE,,,,-1\n")]
    [InlineData(BiomassHeader + "ANE,,,,NaN\n")]
    [InlineData(BiomassHeader + ",,,,1\n")]
    public async Task ReadBiomassesAsync_RejectsInvalidFiles(string content)
    {
        // Arrange
        SetupFile("scenario", "initial_biomasses.csv", content);
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadBiomassesAsync("scenario", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("..\\scenario")]
    [InlineData("../scenario")]
    public async Task ReadBiomassesAsync_RejectsInvalidScenariosBeforeAccessingStorage(string scenario)
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadBiomassesAsync(scenario, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        m_blobStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadBiomassesAsync_PropagatesMissingFile()
    {
        // Arrange
        string path = string.Join('/', "scenario", "initial_biomasses.csv");
        m_blobStore.Setup(store => store.OpenReadAsync(path, PathType.Input, CancellationToken.None))
            .ThrowsAsync(new FileNotFoundException("Missing initial biomass file."));
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadBiomassesAsync("scenario", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadFishingMortalitiesAsync_ReadsIncludedNorthwesternMedFiles()
    {
        // Arrange
        var store = new LocalBlobStore(Path.Combine(AppContext.BaseDirectory, "Includes"), Path.Combine(AppContext.BaseDirectory, "Output"));
        var reader = new InitialStateFileService(store, NullLogger<InitialStateFileService>.Instance);

        // Act
        var biomasses = await reader.ReadBiomassesAsync("northwestern_med", CancellationToken.None);
        var mortalities = await reader.ReadFishingMortalitiesAsync("northwestern_med", CancellationToken.None);

        // Assert
        biomasses.Should().HaveCount(91);
        mortalities.Should().HaveCount(91);
        var species = new IInitialStateFileService.SpeciesKey("ALB", "", "", "");
        biomasses[species].Should().Be(20526.805687297827);
        mortalities[species].Should().Be(9912.18643411314 / 20526.805687297827);
    }

    private InitialStateFileService CreateReader()
    {
        return new InitialStateFileService(m_blobStore.Object, NullLogger<InitialStateFileService>.Instance);
    }

    private void SetupFile(string scenario, string file, string content, CancellationToken cancellationToken = default)
    {
        string path = string.Join('/', scenario, file);
        m_blobStore.Setup(store => store.OpenReadAsync(path, PathType.Input, cancellationToken))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    private static EcologyService CreateEcologyService(IInitialStateFileService reader)
    {
        return new EcologyService(NullLogger<EcologyService>.Instance, Mock.Of<ICheckSimulationService>(), Mock.Of<IEwEController>(), new ProtocolVersionService(), reader);
    }
}
