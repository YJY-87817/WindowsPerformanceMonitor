using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Application.Logging;

/// <summary>
/// Error Log 去重（舊規格 v1.5 §5.6）。依「來源／裝置」分別追蹤：
/// <list type="bullet">
/// <item><c>Error</c>：首次發生、錯誤代碼或訊息改變時寫一筆；相同錯誤持續時，每 10 分鐘寫一筆「過去 10 分鐘又發生 N 次」。</item>
/// <item><c>Unavailable</c>：只在首次發現與內容改變時寫入。</item>
/// <item>從 <c>Error</c>／<c>Unavailable</c> 回到其他狀態時寫一筆恢復紀錄。</item>
/// </list>
/// 執行緒安全。
/// </summary>
public sealed class StatusLogTracker(IAppLog log, IClock clock)
{
    /// <summary>持續錯誤的彙總間隔。</summary>
    public static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(10);

    /// <summary>預設處理方式說明。</summary>
    public const string RetryNextSample = "下次取樣重試";

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>回報一次狀態。</summary>
    /// <param name="source">來源（模組名稱，或 <c>Csv</c> 等）。</param>
    /// <param name="device">裝置（磁碟代號、GPU、介面卡、檔案類型）；模組層級為 null。</param>
    /// <param name="status">本次狀態。</param>
    /// <param name="errorCode">錯誤分類。</param>
    /// <param name="message">錯誤訊息。</param>
    /// <param name="handling">處理方式說明。</param>
    public void Report(string source, string? device, MonitorStatus status, ErrorCode? errorCode, string? message, string handling = RetryNextSample)
    {
        string key = device is null ? source : $"{source}/{device}";
        string label = device is null ? source : $"{source} {device}";
        string detail = string.IsNullOrEmpty(message) ? "（無訊息）" : message;
        TimeSpan now = clock.Elapsed;

        lock (_gate)
        {
            _entries.TryGetValue(key, out Entry? entry);
            switch (status)
            {
                case MonitorStatus.Error:
                    if (entry is null || entry.Status != status || entry.Code != errorCode || entry.Message != message)
                    {
                        log.Write(LogChannel.Error, LogSeverity.Error, source, $"{label} 發生錯誤 [{errorCode}]：{detail}（處理：{handling}）");
                        _entries[key] = new Entry(status, errorCode, message, now);
                    }
                    else
                    {
                        entry.RepeatCount++;
                        if (now - entry.PeriodStart >= SummaryInterval)
                        {
                            log.Write(LogChannel.Error, LogSeverity.Error, source, $"{label} 仍持續錯誤 [{errorCode}]，過去 10 分鐘又發生 {entry.RepeatCount} 次：{detail}");
                            entry.RepeatCount = 0;
                            entry.PeriodStart = now;
                        }
                    }

                    break;

                case MonitorStatus.Unavailable:
                    if (entry is null || entry.Status != status || entry.Code != errorCode || entry.Message != message)
                    {
                        log.Write(LogChannel.Error, LogSeverity.Warn, source, $"{label} 無法使用：{detail}");
                        _entries[key] = new Entry(status, errorCode, message, now);
                    }

                    break;

                default:
                    if (entry is not null)
                    {
                        log.Write(LogChannel.Error, LogSeverity.Info, source, $"{label} 已恢復（{status}）");
                        _entries.Remove(key);
                    }

                    break;
            }
        }
    }

    private sealed class Entry(MonitorStatus status, ErrorCode? code, string? message, TimeSpan periodStart)
    {
        public MonitorStatus Status { get; } = status;

        public ErrorCode? Code { get; } = code;

        public string? Message { get; } = message;

        public TimeSpan PeriodStart { get; set; } = periodStart;

        public int RepeatCount { get; set; }
    }
}
