using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Infrastructure.Storage.Csv;

/// <summary>
/// 將快照寫入 7 種 CSV（程序紀錄只在 <c>includeProcesses</c> 時寫入）。
/// 檔名依快照的本機日期，跨午夜自動寫入新檔。寫入失敗只累計遺失筆數並寫 Error Log（去重），不中止監控。
/// </summary>
public sealed class CsvSnapshotRecorder(
    AppSettings settings,
    AppDataPaths paths,
    CsvFileWriter writer,
    StorageStatus storage,
    StatusLogTracker tracker) : ISnapshotRecorder
{
    private const string Source = "Csv";
    private const string FailureHandling = "略過本次，下個紀錄週期重試";

    /// <inheritdoc />
    public async Task RecordAsync(MonitorSnapshot snapshot, bool includeProcesses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!settings.Logging.Enabled)
        {
            return;
        }

        string directory = paths.ResolveDataDirectory(settings.Logging);
        var localDate = DateOnly.FromDateTime(snapshot.TimestampLocal.DateTime);

        foreach (CsvTable table in CsvTables.All)
        {
            if (table == CsvTables.ProcessMetrics && !includeProcesses)
            {
                continue;
            }

            string[] lines = [.. table.BuildRows(snapshot).Select(CsvFormat.Line)];
            if (lines.Length == 0)
            {
                continue;
            }

            string path = Path.Combine(directory, table.FileName(localDate));
            string? error = await writer.AppendAsync(path, table.Header, lines, settings.Logging.LockedFileRetryCount, cancellationToken).ConfigureAwait(false);
            if (error is null)
            {
                storage.AddWritten(lines.Length);
                tracker.Report(Source, table.Name, MonitorStatus.OK, null, null);
            }
            else
            {
                storage.AddLost(lines.Length, $"{table.Name}：{error}");
                tracker.Report(Source, table.Name, MonitorStatus.Error, ErrorCode.IOError, error, FailureHandling);
            }
        }
    }
}
