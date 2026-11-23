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
using SURIMI.Common.gRPC.Services;
using Xunit;
using EcologyService = Ecopath.Services.EcologyService;

namespace Ecopath.Tests.Services;

public class InitialStateFileServiceTests
{
    private const string Header = "SpeciesCode,LengthClass,Age,LifeStage,Biomass,Catch\n";
    private readonly Mock<IBlobStore> m_blobStore = new(MockBehavior.Strict);

    [Fact]
    public async Task GetInitialHCRData_ReturnsBiomassAndFishingMortalityByFullSpeciesKey()
    {
        // Arrange
        SetupFile("scenario", Header + "ANE,10,2,adult,100,0\nANE,,,juvenile,20,10\n");
        var service = CreateEcologyService(CreateReader());

        // Act
        var response = await service.GetInitialHCRData(new GetInitialHCRDataRequest { ScenarioName = "scenario" }, Mock.Of<ServerCallContext>());

        // Assert
        response.InitialHcrDataSummary.InitialHcrData.Should().HaveCount(2);
        var adult = response.InitialHcrDataSummary.InitialHcrData[0];
        adult.Species.Should().Be(new Species { SpeciesCode = "ANE", LengthClass = "10", Age = "2", LifeStage = "adult" });
        adult.InitialBiomass.Should().Be(100);
        adult.InitialFishingMortality.Should().Be(0);
        var juvenile = response.InitialHcrDataSummary.InitialHcrData[1];
        juvenile.Species.Should().Be(new Species { SpeciesCode = "ANE", LifeStage = "juvenile" });
        juvenile.InitialBiomass.Should().Be(20);
        juvenile.InitialFishingMortality.Should().Be(0.5);
        m_blobStore.VerifyAll();
    }

    [Fact]
    public async Task ReadInitialHCRDataAsync_UsesInvariantCulture()
    {
        // Arrange
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        SetupFile("scenario", Header + "ANE,,,,123.5,61.75\n");
        var reader = CreateReader();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
        try
        {
            // Act
            var values = await reader.ReadInitialHCRDataAsync("scenario", CancellationToken.None);

            // Assert
            values.Values.Should().ContainSingle().Which.Should().Be((123.5, 0.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("0,0")]
    [InlineData("10,")]
    [InlineData("10,-1")]
    [InlineData("10,NaN")]
    [InlineData("10,invalid")]
    [InlineData("1E-300,1E300")]
    public async Task ReadFishingMortalitiesAsync_RejectsUndefinedOrInvalidData(string values)
    {
        // Arrange
        SetupFile("scenario", Header + "ANE,,,," + values + "\n");
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadFishingMortalitiesAsync("scenario", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("SpeciesCode,Biomass\nANE,10\n")]
    [InlineData(Header)]
    [InlineData(Header + "ANE,,,,10,1\nANE,,,,20,2\n")]
    [InlineData(Header + "ANE,,,,-1,0\n")]
    [InlineData(Header + "ANE,,,,NaN,0\n")]
    [InlineData(Header + ",,,,1,0\n")]
    public async Task ReadInitialHCRDataAsync_RejectsInvalidFiles(string content)
    {
        // Arrange
        SetupFile("scenario", content);
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadInitialHCRDataAsync("scenario", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("..\\scenario")]
    [InlineData("../scenario")]
    public async Task ReadInitialHCRDataAsync_RejectsInvalidScenariosBeforeAccessingStorage(string scenario)
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadInitialHCRDataAsync(scenario, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        m_blobStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadInitialHCRDataAsync_PropagatesMissingFile()
    {
        // Arrange
        string path = string.Join('/', "scenario", "InitialHCRData.csv");
        m_blobStore.Setup(store => store.OpenReadAsync(path, PathType.Input, CancellationToken.None))
            .ThrowsAsync(new FileNotFoundException("Missing initial HCR data file."));
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadInitialHCRDataAsync("scenario", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadFishingMortalitiesAsync_ReadsIncludedNorthwesternMedFile()
    {
        // Arrange
        var store = new LocalBlobStore(Path.Combine(AppContext.BaseDirectory, "Includes"), Path.Combine(AppContext.BaseDirectory, "Output"));
        var reader = new InitialStateFileService(store, NullLogger<InitialStateFileService>.Instance);

        // Act
        var initialData = await reader.ReadInitialHCRDataAsync("northwestern_med", CancellationToken.None);
        var mortalities = await reader.ReadFishingMortalitiesAsync("northwestern_med", CancellationToken.None);

        // Assert
        initialData.Should().HaveCount(91);
        mortalities.Should().HaveCount(91);
        var species = new IInitialStateFileService.SpeciesKey("ALB", "", "", "");
        initialData[species].Biomass.Should().Be(20526.805687297827);
        mortalities[species].Should().Be(9912.18643411314 / 20526.805687297827);
    }

    private InitialStateFileService CreateReader()
    {
        return new InitialStateFileService(m_blobStore.Object, NullLogger<InitialStateFileService>.Instance);
    }

    private void SetupFile(string scenario, string content, CancellationToken cancellationToken = default)
    {
        string path = string.Join('/', scenario, "InitialHCRData.csv");
        m_blobStore.Setup(store => store.OpenReadAsync(path, PathType.Input, cancellationToken))
            .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    private static EcologyService CreateEcologyService(IInitialStateFileService reader)
    {
        return new EcologyService(NullLogger<EcologyService>.Instance, Mock.Of<ICheckSimulationService>(), Mock.Of<IEwEController>(), new ProtocolVersionService(), reader);
    }
}
