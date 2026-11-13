using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Storage;

namespace PerformanceMonitor.Infrastructure.Logging;

/// <summary>
/// Application／Error Log：<c>Application_YYYYMMDD.log</c>、<c>Error_YYYYMMDD.log</c>（本機日期），UTF-8 BOM，
/// 一行一筆 <c>yyyy-MM-ddTHH:mm:ss.fffzzz [LEVEL] [來源] 訊息</c>，訊息內換行轉為空白。
/// 寫入失敗只計數，不擲出例外。執行緒安全。
/// </summary>
public sealed class DailyLogWriter(AppSettings settings, AppDataPaths paths, IClock clock, IHostInfo hostInfo) : IAppLog
{
    private readonly Lock _gate = new();
    private int _failureCount;

    /// <inheritdoc />
    public int FailureCount => Volatile.Read(ref _failureCount);

    /// <inheritdoc />
    public void Write(LogChannel channel, LogSeverity severity, string source, string message)
    {
        DateTimeOffset utc = clock.UtcNow;
        DateTimeOffset local = utc.ToOffset(hostInfo.GetUtcOffset(utc));
        string level = severity switch
        {
            LogSeverity.Warn => "WARN",
            LogSeverity.Error => "ERROR",
            _ => "INFO",
        };
        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{local:yyyy-MM-dd'T'HH:mm:ss.fffzzz} [{level}] [{OneLine(source)}] {OneLine(message)}");
        string path = Path.Combine(
            paths.ResolveLogDirectory(settings.Logging),
            string.Create(CultureInfo.InvariantCulture, $"{channel}_{local:yyyyMMdd}.log"));

        lock (_gate)
        {
            try
            {
                TextFileAppender.AppendLines(path, headerLine: null, [line]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                Interlocked.Increment(ref _failureCount);
            }
        }
    }

    private static string OneLine(string? text) =>
        (text ?? string.Empty).Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
