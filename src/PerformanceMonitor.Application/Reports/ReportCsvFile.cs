using System.Globalization;

namespace PerformanceMonitor.Application.Reports;

/// <summary>
/// 報告讀取的 CSV 種類與預期標題列（schema v1，docs/Data-Schema.md §3）。
/// 欄位須與 Infrastructure 的 <c>CsvTables</c> 一致（由 Infrastructure 測試比對）。
/// </summary>
/// <param name="Name">類型名稱。</param>
/// <param name="Version">schema 版本。</param>
/// <param name="Columns">欄位。</param>
public sealed record ReportCsvFile(string Name, int Version, IReadOnlyList<string> Columns)
{
    /// <summary>SystemMetrics。</summary>
    public static readonly ReportCsvFile SystemMetrics = new(
        "SystemMetrics",
        1,
        ["SampleId", "TimestampUtc", "TimestampLocal", "ComputerName", "CpuUsagePercent", "CpuStatus",
         "MemoryTotalGB", "MemoryUsedGB", "MemoryFreeGB", "MemoryUsagePercent", "MemoryStatus",
         "DiskStatus", "DiskIoStatus", "GpuStatus", "NetworkStatus", "ProcessStatus", "TemperatureStatus",
         "SampleDurationMs", "LoopOverrun"]);

    /// <summary>DiskMetrics。</summary>
    public static readonly ReportCsvFile DiskMetrics = new(
        "DiskMetrics", 1, ["SampleId", "TimestampUtc", "Drive", "VolumeLabel", "TotalGB", "UsedGB", "FreeGB", "UsedPercent", "FreePercent", "Status", "ErrorCode"]);

    /// <summary>DiskIoMetrics。</summary>
    public static readonly ReportCsvFile DiskIoMetrics = new(
        "DiskIoMetrics",
        1,
        ["SampleId", "TimestampUtc", "Scope", "Disk", "ReadMBps", "WriteMBps", "ReadIOPS", "WriteIOPS", "ReadLatencyMs", "WriteLatencyMs", "BusyPercent", "Status", "ErrorCode"]);

    /// <summary>GpuMetrics。</summary>
    public static readonly ReportCsvFile GpuMetrics = new(
        "GpuMetrics", 1, ["SampleId", "TimestampUtc", "GpuIndex", "GpuName", "UsagePercent", "MemoryUsedMB", "MemoryTotalMB", "Status", "ErrorCode"]);

    /// <summary>NetworkMetrics。</summary>
    public static readonly ReportCsvFile NetworkMetrics = new(
        "NetworkMetrics",
        1,
        ["SampleId", "TimestampUtc", "AdapterName", "InterfaceDescription", "InterfaceId", "IsPhysical", "ReceivedBytes", "SentBytes", "DownloadMbps", "UploadMbps", "Status", "ErrorCode"]);

    /// <summary>TemperatureMetrics。</summary>
    public static readonly ReportCsvFile TemperatureMetrics = new(
        "TemperatureMetrics", 1, ["SampleId", "TimestampUtc", "SensorType", "SensorName", "TemperatureC", "Status", "ErrorCode"]);

    /// <summary>報告讀取的所有 CSV。</summary>
    public static IReadOnlyList<ReportCsvFile> All { get; } = [SystemMetrics, DiskMetrics, DiskIoMetrics, GpuMetrics, NetworkMetrics, TemperatureMetrics];

    /// <summary>指定日期的檔名。</summary>
    public string FileName(DateOnly localDate) =>
        string.Create(CultureInfo.InvariantCulture, $"{Name}_v{Version}_{localDate:yyyyMMdd}.csv");

    /// <summary>Application Log 檔名。</summary>
    public static string ApplicationLogName(DateOnly localDate) =>
        string.Create(CultureInfo.InvariantCulture, $"Application_{localDate:yyyyMMdd}.log");
}
