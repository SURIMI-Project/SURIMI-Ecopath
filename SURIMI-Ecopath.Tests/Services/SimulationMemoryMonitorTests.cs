using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using Ecopath.EwE;
using Ecopath.Services;
using FluentAssertions;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Surimi;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SURIMI.Common.gRPC.Services;
using Xunit;
using EcologyService = Ecopath.Services.EcologyService;

namespace Ecopath.Tests.Services;

public class SimulationMemoryMonitorTests : IDisposable
{
    private readonly string m_outputDirectory = Path.Combine(Path.GetTempPath(), "SurimiMemoryTests", Guid.NewGuid().ToString("N"));
    private readonly SimulationMemoryMonitor m_monitor;
    private readonly Mock<IEwEController> m_controller = new();
    private readonly Mock<ICheckSimulationService> m_reservations = new();

    public SimulationMemoryMonitorTests()
    {
        m_monitor = new SimulationMemoryMonitor(NullLogger<SimulationMemoryMonitor>.Instance, m_outputDirectory);
    }

    [Fact]
    public void StartRecordAndStop_WriteCsvWithProcessMemoryAndBaselineDeltaInMiB()
    {
        // Arrange
        m_monitor.Start("simulation");

        // Act
        m_monitor.Record("simulation", "Checkpoint");
        m_monitor.Stop("simulation", "FinaliseSimulation");

        // Assert
        var entries = ReadRows();
        entries.Select(entry => entry["Stage"]).Should().Equal("InitialiseSimulation", "Checkpoint", "FinaliseSimulation");
        entries.Should().OnlyContain(entry => (string)entry["SimulationId"] == "simulation");
        entries[0].Keys.Should().Equal("TimestampUtc", "ElapsedSeconds", "SimulationId", "Stage", "WorkingSetMiB", "PrivateMemoryMiB", "PrivateMemoryDeltaMiB", "ManagedHeapMiB", "SampledPeakWorkingSetMiB", "SampledPeakPrivateMemoryMiB", "SampledPeakPrivateMemoryDeltaMiB");
        entries[0]["PrivateMemoryDeltaMiB"].Should().Be(0d);
        entries.Select(entry => (double)entry["ElapsedSeconds"]).Should().BeInAscendingOrder();
        double baseline = (double)entries[0]["PrivateMemoryMiB"];
        foreach (var entry in entries)
        {
            DateTimeOffset.Parse((string)entry["TimestampUtc"], CultureInfo.InvariantCulture).Offset.Should().Be(TimeSpan.Zero);
            ((double)entry["WorkingSetMiB"]).Should().BeGreaterThan(0);
            ((double)entry["PrivateMemoryMiB"]).Should().BeGreaterThan(0);
            ((double)entry["ManagedHeapMiB"]).Should().BeGreaterThan(0);
            ((double)entry["PrivateMemoryDeltaMiB"]).Should().BeApproximately((double)entry["PrivateMemoryMiB"] - baseline, 0.000001);
            ((double)entry["SampledPeakWorkingSetMiB"]).Should().BeGreaterThanOrEqualTo((double)entry["WorkingSetMiB"]);
            ((double)entry["SampledPeakPrivateMemoryMiB"]).Should().BeGreaterThanOrEqualTo((double)entry["PrivateMemoryMiB"]);
            ((double)entry["SampledPeakPrivateMemoryDeltaMiB"]).Should().BeApproximately((double)entry["SampledPeakPrivateMemoryMiB"] - baseline, 0.000001);
        }
        using var exclusiveFile = new FileStream(m_monitor.CsvFilePath!, FileMode.Open, FileAccess.Read, FileShare.None);
        exclusiveFile.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Start_AfterStop_CreatesNewFileAndResetsBaselineAndSampledPeaks()
    {
        // Arrange
        m_monitor.Start("simulation");
        m_monitor.Stop("simulation", "FinaliseSimulation");
        string? firstFile = m_monitor.CsvFilePath;

        // Act
        m_monitor.Start("simulation");

        // Assert
        m_monitor.CsvFilePath.Should().NotBe(firstFile);
        File.Exists(firstFile).Should().BeTrue();
        var baseline = ReadRows().Single();
        baseline["SimulationId"].Should().Be("simulation");
        baseline["PrivateMemoryDeltaMiB"].Should().Be(0d);
        baseline["SampledPeakPrivateMemoryDeltaMiB"].Should().Be(0d);
        baseline["SampledPeakPrivateMemoryMiB"].Should().Be(baseline["PrivateMemoryMiB"]);
        baseline["SampledPeakWorkingSetMiB"].Should().Be(baseline["WorkingSetMiB"]);
    }

    [Fact]
    public void Csv_UsesInvariantNumbersAndEscapesText()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        const string simulationId = "simulation,\"quoted\"";
        const string stage = "Checkpoint,\"quoted\"\r\nnext line";
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
        try
        {
            // Act
            m_monitor.Start(simulationId);
            m_monitor.Stop(simulationId, stage);

            // Assert
            var rows = ReadRows();
            rows.Last()["SimulationId"].Should().Be(simulationId);
            rows.Last()["Stage"].Should().Be(stage);
            ((double)rows.Last()["PrivateMemoryMiB"]).Should().BeGreaterThan(0);
            Path.GetDirectoryName(m_monitor.CsvFilePath).Should().Be(m_outputDirectory);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task Monitoring_WritesFlushedSamplesWhileRunning_AndStopsAfterFinalization()
    {
        // Arrange
        m_monitor.Start("simulation");
        var timeout = Stopwatch.StartNew();

        // Act
        while (!ReadRows().Any(entry => (string)entry["Stage"] == "Running") && timeout.Elapsed < TimeSpan.FromSeconds(10))
            await Task.Delay(100);
        m_monitor.Stop("simulation", "FinaliseSimulation");
        long lengthAfterStop = new FileInfo(m_monitor.CsvFilePath!).Length;
        await Task.Delay(1200);

        // Assert
        var sample = ReadRows().First(entry => (string)entry["Stage"] == "Running");
        sample["SimulationId"].Should().Be("simulation");
        ((double)sample["ElapsedSeconds"]).Should().BeGreaterThanOrEqualTo(0.9);
        ((double)sample["SampledPeakPrivateMemoryMiB"]).Should().BeGreaterThanOrEqualTo((double)sample["PrivateMemoryMiB"]);
        new FileInfo(m_monitor.CsvFilePath!).Length.Should().Be(lengthAfterStop);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InitialiseSimulation_WritesBaselineAndInitializationOutcome(bool succeeds)
    {
        // Arrange
        var service = CreateService();
        var request = JsonParser.Default.Parse<InitialiseSimulationRequest>("""
            {
                "simulationId": "simulation",
                "scenarioName": "scenario",
                "simulation": {
                    "startDateTime": "2013-01-01T00:00:00Z",
                    "maximumEndDateTime": "2050-01-01T00:00:00Z",
                    "geography": { "rasterCellOrigin": 1, "crs": {} },
                    "standards": { "measurements": {} },
                    "items": {}
                }
            }
            """);
        m_controller.Setup(controller => controller.StartAsync(It.IsAny<SURIMI.Datamodel.SurimiContract>(), "scenario", "", It.IsAny<CancellationToken>()))
            .Callback(() => ReadRows().Single()["Stage"].Should().Be("InitialiseSimulation"))
            .ReturnsAsync(succeeds ? 1 : -1);

        // Act
        var action = () => service.InitialiseSimulation(request, Mock.Of<ServerCallContext>());
        if (succeeds)
            await action();
        else
            await action.Should().ThrowAsync<RpcException>();

        // Assert
        ReadRows().Select(entry => entry["Stage"]).Should().Equal("InitialiseSimulation", succeeds ? "Initialised" : "InitialisationFailed");
        m_reservations.Verify(service => service.ReleaseSimulation("simulation"), succeeds ? Times.Never() : Times.Once());
        if (!succeeds)
        {
            m_monitor.Start("next");
            ReadRows().Single()["PrivateMemoryDeltaMiB"].Should().Be(0d);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EndSimulation_WritesBeforeAndAfterTeardown_AndClosesCsv(bool cancel, bool fails)
    {
        // Arrange
        m_monitor.Start("simulation");
        var service = CreateService();
        m_controller.Setup(controller => controller.StopAsync(10000))
            .Callback(() =>
            {
                ReadRows().Last()["Stage"].Should().Be(cancel ? "Cancelling" : "Finalising");
                m_reservations.Verify(reservations => reservations.ReleaseSimulation("simulation"), Times.Never());
            })
            .ReturnsAsync(!fails);

        // Act
        Func<Task> action = async () =>
        {
            if (cancel)
                await service.CancelSimulation(new CancelSimulationRequest { SimulationId = "simulation" }, Mock.Of<ServerCallContext>());
            else
                await service.FinaliseSimulation(new FinaliseSimulationRequest { SimulationId = "simulation" }, Mock.Of<ServerCallContext>());
        };
        if (fails)
            await action.Should().ThrowAsync<RpcException>();
        else
            await action();

        // Assert
        ReadRows().Select(entry => entry["Stage"]).Should().Equal("InitialiseSimulation", cancel ? "Cancelling" : "Finalising", cancel ? "CancelSimulation" : "FinaliseSimulation");
        m_reservations.Verify(reservations => reservations.ReleaseSimulation("simulation"), Times.Once());
        m_monitor.Start("next");
        ReadRows().Single()["SimulationId"].Should().Be("next");
    }

    [Fact]
    public async Task SimulateStep_WritesMemoryAfterControllerReturns()
    {
        // Arrange
        m_monitor.Start("simulation");
        m_controller.Setup(controller => controller.ContinueAsync(60000)).ReturnsAsync(true);
        var service = CreateService();

        // Act
        var response = await service.SimulateStep(new SimulateStepRequest { SimulationId = "simulation" }, Mock.Of<ServerCallContext>());

        // Assert
        response.SimulationId.Should().Be("simulation");
        ReadRows().Last()["Stage"].Should().Be("AfterSimulateStep");
        m_controller.Verify(controller => controller.ContinueAsync(60000), Times.Once());
    }

    private Dictionary<string, object>[] ReadRows()
    {
        using var stream = new FileStream(m_monitor.CsvFilePath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var entries = new List<Dictionary<string, object>>();
        while (csv.Read())
        {
            entries.Add(csv.HeaderRecord!.ToDictionary(header => header, header =>
                header is "TimestampUtc" or "SimulationId" or "Stage"
                    ? (object)csv.GetField<string>(header)!
                    : csv.GetField<double>(header)));
        }
        return entries.ToArray();
    }

    private EcologyService CreateService()
    {
        return new EcologyService(NullLogger<EcologyService>.Instance, m_reservations.Object, m_controller.Object,
            new ProtocolVersionService(), Mock.Of<IInitialStateFileService>(), m_monitor);
    }

    public void Dispose()
    {
        m_monitor.Dispose();
        if (Directory.Exists(m_outputDirectory))
            Directory.Delete(m_outputDirectory, true);
    }
}