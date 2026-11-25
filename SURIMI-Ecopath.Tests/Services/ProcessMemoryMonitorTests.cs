using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using Ecopath.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ecopath.Tests.Services;

public class ProcessMemoryMonitorTests : IDisposable
{
    private readonly string m_outputDirectory = Path.Combine(Path.GetTempPath(), "SurimiMemoryTests", Guid.NewGuid().ToString("N"));
    private readonly ProcessMemoryMonitor m_monitor;

    public ProcessMemoryMonitorTests()
    {
        m_monitor = new ProcessMemoryMonitor(NullLogger<ProcessMemoryMonitor>.Instance, m_outputDirectory);
    }

    [Fact]
    public async Task StartAndDispose_WriteCsvWithProcessMemoryAndBaselineDeltaInMiB()
    {
        // Arrange
        // Act
        await m_monitor.StartAsync(CancellationToken.None);
        m_monitor.Dispose();

        // Assert
        var entries = ReadRows();
        entries.Select(entry => entry["Stage"]).Should().Equal("ProcessStarted", "ProcessStopped");
        entries[0].Keys.Should().Equal("TimestampUtc", "ElapsedSeconds", "Stage", "WorkingSetMiB", "PrivateMemoryMiB", "PrivateMemoryDeltaMiB", "ManagedHeapMiB", "SampledPeakWorkingSetMiB", "SampledPeakPrivateMemoryMiB", "SampledPeakPrivateMemoryDeltaMiB");
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
        Path.GetDirectoryName(m_monitor.CsvFilePath).Should().Be(m_outputDirectory);
    }

    [Fact]
    public async Task Monitoring_WritesFlushedSamplesPeriodically_UntilDisposed()
    {
        // Arrange
        await m_monitor.StartAsync(CancellationToken.None);
        var timeout = Stopwatch.StartNew();

        // Act
        while (ReadRows().Count(entry => (string)entry["Stage"] == "Running") < 2 && timeout.Elapsed < TimeSpan.FromSeconds(10))
            await Task.Delay(100);
        m_monitor.Dispose();
        long lengthAfterDispose = new FileInfo(m_monitor.CsvFilePath!).Length;
        await Task.Delay(1200);

        // Assert
        var entries = ReadRows();
        var stages = entries.Select(entry => (string)entry["Stage"]).ToArray();
        stages.First().Should().Be("ProcessStarted");
        stages.Last().Should().Be("ProcessStopped");
        stages.Count(stage => stage == "Running").Should().BeGreaterThanOrEqualTo(2);
        ((double)entries[1]["ElapsedSeconds"]).Should().BeGreaterThanOrEqualTo(0.9);
        new FileInfo(m_monitor.CsvFilePath!).Length.Should().Be(lengthAfterDispose);
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_KeepsWritingToTheSameFile()
    {
        // Arrange
        await m_monitor.StartAsync(CancellationToken.None);
        string? csvFile = m_monitor.CsvFilePath;

        // Act
        await m_monitor.StartAsync(CancellationToken.None);

        // Assert
        m_monitor.CsvFilePath.Should().Be(csvFile);
        Directory.GetFiles(m_outputDirectory).Should().ContainSingle();
    }

    [Fact]
    public async Task Csv_UsesInvariantNumbers()
    {
        // Arrange
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");
        try
        {
            // Act
            await m_monitor.StartAsync(CancellationToken.None);
            m_monitor.Dispose();

            // Assert
            ReadRows().Should().HaveCount(2);
            ((double)ReadRows()[0]["PrivateMemoryMiB"]).Should().BeGreaterThan(0);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task Dispose_DoesNotWriteAgain_WhenCalledTwice()
    {
        // Arrange
        await m_monitor.StartAsync(CancellationToken.None);
        m_monitor.Dispose();

        // Act
        m_monitor.Dispose();

        // Assert
        ReadRows().Select(entry => entry["Stage"]).Should().Equal("ProcessStarted", "ProcessStopped");
    }

    [Fact]
    public void Dispose_DoesNothing_WhenNotStarted()
    {
        // Arrange
        // Act
        m_monitor.Dispose();

        // Assert
        m_monitor.CsvFilePath.Should().BeNull();
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
                header is "TimestampUtc" or "Stage"
                    ? (object)csv.GetField<string>(header)!
                    : csv.GetField<double>(header)));
        }
        return entries.ToArray();
    }

    public void Dispose()
    {
        m_monitor.Dispose();
        if (Directory.Exists(m_outputDirectory))
            Directory.Delete(m_outputDirectory, true);
    }
}
