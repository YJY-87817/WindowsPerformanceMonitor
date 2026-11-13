using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Application.Output;

/// <summary>
/// 主控台輸出：易讀文字，或完整 JSON（<c>--once --json</c>，取代舊版 <c>-PassThru</c> 供腳本使用）。
/// 數值以 Invariant Culture 輸出；無數值顯示 <c>N/A</c>（文字）或 null（JSON）。
/// </summary>
public static class SnapshotFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,

        // 中文等字元直接輸出（不轉為 \uXXXX）；HTML 特殊字元仍會跳脫。
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>JSON：快照、各模組完整結果與 CSV 寫入統計。</summary>
    public static string ToJson(MonitorSnapshot snapshot, IStorageStatus storage)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(storage);
        var document = new
        {
            SampleId = snapshot.SampleId.ToString(),
            snapshot.TimestampUtc,
            snapshot.TimestampLocal,
            snapshot.ComputerName,
            SampleDurationMs = Math.Round(snapshot.SampleDuration.TotalMilliseconds, 3),
            snapshot.LoopOverrun,
            Modules = snapshot.Results.Select(r => (object)r).ToArray(),
            Csv = new { storage.WrittenRows, storage.LostRows, storage.LastError },
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>易讀文字。</summary>
    public static string ToText(MonitorSnapshot snapshot, IStorageStatus storage)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(storage);
        var text = new StringBuilder();
        Line(text, $"Time: {snapshot.TimestampLocal:yyyy-MM-dd'T'HH:mm:sszzz}  Computer: {snapshot.ComputerName}");
        Line(text, $"SampleId: {snapshot.SampleId}");
        text.AppendLine();
        foreach (ModuleResult result in snapshot.Results)
        {
            string detail = Detail(result);
            Line(text, $"{result.Module,-12} {result.Status,-12} {detail}".TrimEnd());
        }

        text.AppendLine();
        Line(text, $"Duration: {snapshot.SampleDuration.TotalMilliseconds:0} ms  LoopOverrun: {snapshot.LoopOverrun}");
        Line(text, $"CSV: written {storage.WrittenRows}, lost {storage.LostRows}{(storage.LastError is null ? string.Empty : $" ({storage.LastError})")}");
        return text.ToString();
    }

    private static string Detail(ModuleResult result)
    {
        string? value = result switch
        {
            CpuResult cpu => Percent(cpu.UsagePercent),
            MemoryResult memory => memory.UsagePercent is null
                ? "N/A"
                : $"{Percent(memory.UsagePercent)} ({Number(memory.UsedGB, "0.00")} / {Number(memory.TotalGB, "0.00")} GB)",
            DiskResult disk => string.Join("; ", disk.Disks.Select(d => d.Status == Core.Enums.MonitorStatus.OK
                ? $"{d.Drive} {Percent(d.UsedPercent)} used, {Number(d.FreeGB, "0.00")} GB free"
                : $"{d.Drive} {d.Status}")),
            DiskIoResult io => string.Join("; ", io.Disks.Select(d => d.Status == Core.Enums.MonitorStatus.OK
                ? $"{d.DeviceId} R {Number(d.ReadMBps, "0.00")} / W {Number(d.WriteMBps, "0.00")} MB/s, busy {Percent(d.BusyPercent)}"
                : $"{d.DeviceId} {d.Status}")),
            GpuResult gpu => string.Join("; ", gpu.Gpus.Select(g => g.Status == Core.Enums.MonitorStatus.OK
                ? $"{g.DeviceId} {g.GpuName} {Percent(g.UsagePercent)}, {Number(g.MemoryUsedMB, "0")} / {Number(g.MemoryTotalMB, "0")} MB, {Number(g.TemperatureC, "0")}°C"
                : $"{g.DeviceId} {g.Status}")),
            ProcessResult process => string.Join(", ", process.Processes.Take(3).Select(p => $"{p.ProcessName} {Number(p.WorkingSetMB, "0.0")} MB")),
            _ => null,
        };
        string? error = result.ErrorMessage is null ? null : $"[{result.ErrorCode}] {result.ErrorMessage}";
        return string.Join("  ", new[] { value, error }.Where(s => s is not null));
    }

    private static string Number(double? value, string format) =>
        value is double v ? v.ToString(format, CultureInfo.InvariantCulture) : "N/A";

    private static string Percent(double? value) =>
        value is double v ? v.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "N/A";

    private static void Line(StringBuilder text, FormattableString line) =>
        text.Append(line.ToString(CultureInfo.InvariantCulture)).Append('\n');

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
