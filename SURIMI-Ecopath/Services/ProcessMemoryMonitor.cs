using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;

namespace Ecopath.Services;

/// <summary>
/// Monitors the memory of the SURIMI-Ecopath process for as long as the host runs.
/// One CSV is written from host start until the host is disposed, so the memory use can be related to the simulations logged in the same period.
/// </summary>
public sealed class ProcessMemoryMonitor : IHostedService, IDisposable
{
    private const double BytesPerMiB = 1024d * 1024d;
    private static readonly TimeSpan s_sampleInterval = TimeSpan.FromSeconds(1);
    private readonly ILogger<ProcessMemoryMonitor> m_logger;
    private readonly string m_outputDirectory;
    private readonly Lock m_lock = new();
    private Timer? m_timer;
    private CsvWriter? m_csv;
    private long m_startedAt;
    private long m_baselinePrivateMemory;
    private long m_peakWorkingSet;
    private long m_peakPrivateMemory;

    public ProcessMemoryMonitor(ILogger<ProcessMemoryMonitor> logger, string? outputDirectory = null)
    {
        m_logger = logger;
        m_outputDirectory = outputDirectory ?? Path.Combine(AppContext.BaseDirectory, "Output", "memory");
    }

    public string? CsvFilePath { get; private set; }

    /// <summary>
    /// Opens the CSV, takes the baseline measurement and starts sampling the memory every second.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (m_lock)
        {
            if (m_csv is not null)
                return Task.CompletedTask;

            var sample = ReadMemory();
            OpenCsv(sample);
            m_logger.LogInformation("Process memory monitoring started. Memory CSV: {CsvFilePath}", CsvFilePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Does nothing: sampling continues until <see cref="Dispose"/>, so the memory use during the shutdown is measured too.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a final measurement, then closes the CSV and stops sampling.
    /// </summary>
    public void Dispose()
    {
        lock (m_lock)
        {
            if (m_csv is null)
                return;

            try
            {
                WriteSample("ProcessStopped", ReadMemory());
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                m_logger.LogError(ex, "Could not write final memory sample to {CsvFilePath}", CsvFilePath);
            }

            CloseCsv();
            m_logger.LogInformation("Process memory monitoring stopped. Memory CSV: {CsvFilePath}", CsvFilePath);
        }
    }

    private void OpenCsv(MemorySample sample)
    {
        Directory.CreateDirectory(m_outputDirectory);
        CsvFilePath = Path.GetFullPath(Path.Combine(m_outputDirectory, $"process-memory-{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.csv"));
        var stream = new FileStream(CsvFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        m_csv = new CsvWriter(new StreamWriter(stream, new UTF8Encoding(true)), CultureInfo.InvariantCulture);
        m_startedAt = Stopwatch.GetTimestamp();
        m_baselinePrivateMemory = sample.PrivateMemory;
        m_peakWorkingSet = sample.WorkingSet;
        m_peakPrivateMemory = sample.PrivateMemory;
        try
        {
            m_csv.WriteHeader<MemoryCsvRow>();
            m_csv.NextRecord();
            WriteSample("ProcessStarted", sample);
            m_timer = new Timer(SampleMemory, null, s_sampleInterval, s_sampleInterval);
        }
        catch
        {
            CloseCsv();
            throw;
        }
    }

    private void SampleMemory(object? state)
    {
        lock (m_lock)
        {
            if (m_csv is null)
                return;

            try
            {
                WriteSample("Running", ReadMemory());
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                m_logger.LogError(ex, "Could not write memory sample to {CsvFilePath}", CsvFilePath);
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
            stage,
            sample.WorkingSet / BytesPerMiB, sample.PrivateMemory / BytesPerMiB,
            (sample.PrivateMemory - m_baselinePrivateMemory) / BytesPerMiB, sample.ManagedHeap / BytesPerMiB,
            m_peakWorkingSet / BytesPerMiB, m_peakPrivateMemory / BytesPerMiB,
            (m_peakPrivateMemory - m_baselinePrivateMemory) / BytesPerMiB));
        m_csv.NextRecord();
        m_csv.Flush();
    }

    private void CloseCsv()
    {
        m_timer?.Dispose();
        m_timer = null;
        var csv = m_csv;
        m_csv = null;
        csv?.Dispose();
    }

    private readonly record struct MemorySample(long WorkingSet, long PrivateMemory, long ManagedHeap);

    private sealed record MemoryCsvRow(
        string TimestampUtc,
        double ElapsedSeconds,
        string Stage,
        double WorkingSetMiB,
        double PrivateMemoryMiB,
        double PrivateMemoryDeltaMiB,
        double ManagedHeapMiB,
        double SampledPeakWorkingSetMiB,
        double SampledPeakPrivateMemoryMiB,
        double SampledPeakPrivateMemoryDeltaMiB);
}
