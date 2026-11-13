using System.Globalization;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Presentation.Text;

/// <summary>各模組結果的一行摘要（總覽卡片使用）。數值以 Invariant Culture 顯示；無法取得時不顯示假數值。</summary>
public static class ModuleSummary
{
    /// <summary>總覽最多列出幾個程序。</summary>
    public const int OverviewProcessCount = 3;

    /// <summary>摘要；沒有可顯示的數值時為 <see cref="UiText.NoValue"/>。</summary>
    public static string For(ModuleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string? text = result switch
        {
            CpuResult cpu => Percent(cpu.UsagePercent),
            MemoryResult m when m.UsagePercent is not null => $"{Percent(m.UsagePercent)}（{Number(m.UsedGB, "0.00")} / {Number(m.TotalGB, "0.00")} GB）",
            DiskResult disk => Join(disk.Disks, d => d.Status == MonitorStatus.OK
                ? $"{d.Drive} {Percent(d.UsedPercent)}（可用 {Number(d.FreeGB, "0.00")} GB）"
                : Device(d.Drive, d.Status)),
            DiskIoResult io => Join(io.Disks.Where(d => d.Scope == DiskIoScope.Logical), d => d.Status == MonitorStatus.OK
                ? $"{d.Disk} 讀 {Number(d.ReadMBps, "0.00")} / 寫 {Number(d.WriteMBps, "0.00")} MB/s，忙碌 {Percent(d.BusyPercent)}"
                : Device(d.Disk, d.Status)),
            GpuResult gpu => Join(gpu.Gpus, g => g.Status == MonitorStatus.OK
                ? $"{g.DeviceId} {g.GpuName}：{Percent(g.UsagePercent)}，顯存 {Number(g.MemoryUsedMB, "0")} / {Number(g.MemoryTotalMB, "0")} MB，{Number(g.TemperatureC, "0")}°C"
                : Device(g.DeviceId, g.Status)),
            NetworkResult n when n.Adapters.Count > 0 =>
                $"下載 {Number(n.TotalDownloadMbps, "0.00")} / 上傳 {Number(n.TotalUploadMbps, "0.00")} Mbps（{n.Adapters.Count(a => a.Status == MonitorStatus.OK)} / {n.Adapters.Count} 張介面卡）",
            ProcessResult p => Join(p.Processes.Take(OverviewProcessCount), x => $"{x.ProcessName} {Number(x.WorkingSetMB, "0.0")} MB"),
            TemperatureResult t => Join(t.Sensors, z => z.Status == MonitorStatus.OK ? $"{z.SensorName} {Number(z.TemperatureC, "0.0")}°C" : Device(z.SensorName, z.Status)),
            _ => null,
        };
        return string.IsNullOrEmpty(text) ? UiText.NoValue : text;
    }

    /// <summary>數值文字；null 為 <see cref="UiText.NoValue"/>。</summary>
    public static string Number(double? value, string format) =>
        value is double v ? v.ToString(format, CultureInfo.InvariantCulture) : UiText.NoValue;

    /// <summary>百分比文字（1 位小數）。</summary>
    public static string Percent(double? value) =>
        value is double v ? v.ToString("0.0", CultureInfo.InvariantCulture) + "%" : UiText.NoValue;

    private static string Device(string name, MonitorStatus status) => $"{name} {UiText.Status(status)}";

    private static string Join<T>(IEnumerable<T> items, Func<T, string> format) => string.Join("；", items.Select(format));
}
