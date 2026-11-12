using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;

namespace Ecopath.Services;

public sealed class SimulationMemoryMonitor : IDisposable
{
    private const double BytesPerMiB = 1024d * 1024d;
    private readonly ILogger<SimulationMemoryMonitor> m_logger;
    private readonly string m_outputDirectory;
    private readonly Lock m_lock = new();
    private Timer? m_timer;
    private CsvWriter? m_csv;
    private string? m_simulationId;
    private long m_startedAt;
    private long m_baselinePrivateMemory;
    private long m_peakWorkingSet;
    private long m_peakPrivateMemory;

    public SimulationMemoryMonitor(ILogger<SimulationMemoryMonitor> logger, string? outputDirectory = null)
    {
        m_logger = logger;
        m_outputDirectory = outputDirectory ?? Path.Combine(AppContext.BaseDirectory, "Output", "memory");
    }

    public string? CsvFilePath { get; private set; }

    public void Start(string simulationId)
    {
        lock (m_lock)
        {
            if (m_simulationId != null)
                throw new InvalidOperationException("Simulation memory monitoring is already active.");

            var sample = ReadMemory();
            Directory.CreateDirectory(m_outputDirectory);
            CsvFilePath = Path.GetFullPath(Path.Combine(m_outputDirectory, $"simulation-memory-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.csv"));
            var stream = new FileStream(CsvFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            m_csv = new CsvWriter(new StreamWriter(stream, new UTF8Encoding(true)), CultureInfo.InvariantCulture);
            m_simulationId = simulationId;
            m_startedAt = Stopwatch.GetTimestamp();
            m_baselinePrivateMemory = sample.PrivateMemory;
            m_peakWorkingSet = sample.WorkingSet;
            m_peakPrivateMemory = sample.PrivateMemory;
            try
            {
                m_csv.WriteHeader<MemoryCsvRow>();
                m_csv.NextRecord();
                WriteSample("InitialiseSimulation", sample);
                m_timer = new Timer(SampleMemory, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
            catch
            {
                Dispose();
                throw;
            }
            m_logger.LogInformation("Writing memory measurements for simulation {SimulationId} to {CsvFilePath}", simulationId, CsvFilePath);
        }
    }

    public void Record(string simulationId, string stage)
    {
        lock (m_lock)
        {
            if (m_simulationId != simulationId)
            {
                m_logger.LogWarning("Cannot record memory for simulation {SimulationId}: it is not being monitored", simulationId);
                return;
            }

            WriteSample(stage, ReadMemory());
        }
    }

    public void Stop(string simulationId, string stage)
    {
        lock (m_lock)
        {
            if (m_simulationId != simulationId)
            {
                m_logger.LogWarning("Cannot stop memory monitoring for simulation {SimulationId}: it is not being monitored", simulationId);
                return;
            }

            try
            {
                WriteSample(stage, ReadMemory());
            }
            finally
            {
                Dispose();
            }
            m_logger.LogInformation("Completed memory CSV for simulation {SimulationId}: {CsvFilePath}", simulationId, CsvFilePath);
        }
    }

    private void SampleMemory(object? state)
    {
        lock (m_lock)
        {
            if (m_simulationId == null)
                return;

            try
            {
                WriteSample("Running", ReadMemory());
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                m_logger.LogError(ex, "Could not write memory sample for simulation {SimulationId} to {CsvFilePath}", m_simulationId, CsvFilePath);
            }
        }
    }

    private static MemorySample ReadMemory()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new MemorySample(process.WorkingSet64, process.PrivateMemorySize64, GC.GetTotalMemory(false));
    }

    private void WriteSample(string stage, MemorySample sample)
    {
        m_peakWorkingSet = Math.Max(m_peakWorkingSet, sample.WorkingSet);
        m_peakPrivateMemory = Math.Max(m_peakPrivateMemory, sample.PrivateMemory);
        m_csv!.WriteRecord(new MemoryCsvRow(
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Stopwatch.GetElapsedTime(m_startedAt).TotalSeconds,
            m_simulationId!, stage,
            sample.WorkingSet / BytesPerMiB, sample.PrivateMemory / BytesPerMiB,
            (sample.PrivateMemory - m_baselinePrivateMemory) / BytesPerMiB, sample.ManagedHeap / BytesPerMiB,
            m_peakWorkingSet / BytesPerMiB, m_peakPrivateMemory / BytesPerMiB,
            (m_peakPrivateMemory - m_baselinePrivateMemory) / BytesPerMiB));
        m_csv.NextRecord();
        m_csv.Flush();
    }

    public void Dispose()
    {
        lock (m_lock)
        {
            m_timer?.Dispose();
            m_timer = null;
            m_simulationId = null;
            var csv = m_csv;
            m_csv = null;
            csv?.Dispose();
        }
    }

    private readonly record struct MemorySample(long WorkingSet, long PrivateMemory, long ManagedHeap);

    private sealed record MemoryCsvRow(
        string TimestampUtc,
        double ElapsedSeconds,
        string SimulationId,
        string Stage,
        double WorkingSetMiB,
        double PrivateMemoryMiB,
        double PrivateMemoryDeltaMiB,
        double ManagedHeapMiB,
        double SampledPeakWorkingSetMiB,
        double SampledPeakPrivateMemoryMiB,
        double SampledPeakPrivateMemoryDeltaMiB);
}