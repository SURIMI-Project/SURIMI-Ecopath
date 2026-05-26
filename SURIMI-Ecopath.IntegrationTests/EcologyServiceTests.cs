using FluentAssertions;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Surimi;
using Xunit;

namespace Ecopath.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="Ecopath.Services.EcologyService"/>.
/// The test host is started in-process via <see cref="EcopathWebApplicationFactory"/>.
/// Heavy EwE dependencies should be replaced with test doubles in the factory
/// before these tests can run without a full EwE installation.
/// </summary>
public class EcologyServiceTests : IClassFixture<EcopathWebApplicationFactory>
{
    private readonly EcopathWebApplicationFactory _factory;

    public EcologyServiceTests(EcopathWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private EcologyService.EcologyServiceClient CreateClient()
    {
        var httpClient = _factory.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
        return new EcologyService.EcologyServiceClient(channel);
    }

    /// <summary>
    /// Loads a Protobuf message from a JSON fixture file.
    /// <paramref name="relativeJsonPath"/> is relative to the <c>GrpcMessages</c> directory,
    /// e.g. <c>InitialiseSimulation\EcologyService_InitialiseSimulation.json</c>.
    /// </summary>
    private static T LoadMessage<T>(string relativeJsonPath) where T : Google.Protobuf.IMessage<T>, new()
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, "GrpcMessages", relativeJsonPath);
        var json = File.ReadAllText(fullPath);
        var parser = new Google.Protobuf.JsonParser(Google.Protobuf.JsonParser.Settings.Default);
        return parser.Parse<T>(json);
    }

    [Fact]
    public async Task SimulationFlow_SendsMessagesInOrder_ReturnsBiomassWithExpectedValues()
    {
        var client = CreateClient();

        // 1. Initialise simulation
        var initialiseRequest = LoadMessage<InitialiseSimulationRequest>(
            @"InitialiseSimulation\EcologyService_InitialiseSimulation.json");
        var initialiseResponse = await client.InitialiseSimulationAsync(initialiseRequest);
        initialiseResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");

        // 2. Update species prices
        var updatePricesRequest = LoadMessage<UpdateSpeciesPricesRequest>(
            @"UpdateSpeciesPrices\EcologyService_UpdateSpeciesPrices.json");
        var updatePricesResponse = await client.UpdateSpeciesPricesAsync(updatePricesRequest);
        updatePricesResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");

        // 3. Simulate a time step
        var simulateStepRequest = LoadMessage<SimulateStepRequest>(
            @"SimulateStep\EcologyService_SimulateStep.json");
        var simulateStepResponse = await client.SimulateStepAsync(simulateStepRequest);
        simulateStepResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");

        // 4. Get biomass and verify the response
        var getBiomassRequest = LoadMessage<GetBiomassRequest>(
            @"GetBiomass\EcologyService_GetBiomass.json");
        var getBiomassResponse = await client.GetBiomassAsync(getBiomassRequest);
        getBiomassResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");
        getBiomassResponse.BiomassSummary.BiomassGrids.Count.Should().Be(8);

        // 5. Get catch disposition and verify the response
        var getCatchDispositionRequest = LoadMessage<GetCatchDispositionRequest>(
            @"GetCatchDisposition\EcologyService_GetCatchDisposition.json");
        var getCatchDispositionResponse = await client.GetCatchDispositionAsync(getCatchDispositionRequest);
        getCatchDispositionResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");
        getCatchDispositionResponse.CatchDispositionSummary.DispositionGrids.Count.Should().Be(46);

        // 6. Get sales and verify the response
        var getSalesRequest = LoadMessage<GetSalesRequest>(
            @"GetSales\EcologyService_GetSales.json");
        var getSalesResponse = await client.GetSalesAsync(getSalesRequest);
        getSalesResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");
        getSalesResponse.SalesSummary.MarketSales.Count.Should().Be(0);

        // 7. Get fishing activity and verify the response
        var getFishingActivityRequest = LoadMessage<GetFishingActivityRequest>(
            @"GetFishingActivity\EcologyService_GetFishingActivity.json");
        var getFishingActivityResponse = await client.GetFishingActivityAsync(getFishingActivityRequest);
        getFishingActivityResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");
        getFishingActivityResponse.FishingActivitySummary.FishingActivities.Count.Should().Be(2);


        // 5. Finalise simulation
        var finaliseRequest = LoadMessage<FinaliseSimulationRequest>(
            @"FinaliseSimulation\EcologyService_FinaliseSimulation.json");
        var finaliseResponse = await client.FinaliseSimulationAsync(finaliseRequest);
        finaliseResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");
    }

    [Fact]
    public async Task CancelSimulation_AfterInitialise_ReturnsSimulationId()
    {
        var client = CreateClient();

        // Initialise first so there is an active simulation to cancel
        var initialiseRequest = LoadMessage<InitialiseSimulationRequest>(
            @"InitialiseSimulation\EcologyService_InitialiseSimulation.json");
        await client.InitialiseSimulationAsync(initialiseRequest);

        // Cancel the simulation
        var cancelRequest = LoadMessage<CancelSimulationRequest>(
            @"CancelSimulation\EcologyService_CancelSimulation.json");
        var cancelResponse = await client.CancelSimulationAsync(cancelRequest);

        cancelResponse.SimulationId.Should().Be("123e4567-e89b-12d3-a456-426614174000");
    }

    [Fact]
    public async Task InitialiseSimulation_WithEmptyScenarioName_ReturnsInvalidArgument()
    {
        var client = CreateClient();

        // Build a request with an empty ScenarioName, which fails GrpcValidation.ArgumentNotNullOrEmpty
        var request = LoadMessage<InitialiseSimulationRequest>(
            @"InitialiseSimulation\EcologyService_InitialiseSimulation_BadRequest.json");
        request = request.Clone();
        request.ScenarioName = string.Empty;

        var act = async () => await client.InitialiseSimulationAsync(request);

        // gRPC maps HTTP 400 Bad Request to StatusCode.InvalidArgument
        var ex = await act.Should().ThrowAsync<RpcException>();
        ex.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);

        // Unpack the rich BadRequest detail and check the field violation description
        var badRequest = ex.Which.GetRpcStatus()?
            .Details
            .Select(any => any.TryUnpack<BadRequest>(out var br) ? br : null)
            .FirstOrDefault(br => br is not null);
        badRequest.Should().NotBeNull();
        badRequest!.FieldViolations
            .Should().ContainSingle(v => v.Description == "Value is null or empty");
    }
}
