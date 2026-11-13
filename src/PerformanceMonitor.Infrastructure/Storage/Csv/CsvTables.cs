using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using static PerformanceMonitor.Infrastructure.Storage.Csv.CsvFormat;

namespace PerformanceMonitor.Infrastructure.Storage.Csv;

/// <summary>
/// schema v1 的 7 種 CSV（docs/Data-Schema.md §3）。分項檔在模組有結果但沒有任何項目時，
/// 寫一筆只有 SampleId、TimestampUtc、Status（與 ErrorCode）的狀態列；快照中沒有該模組時不寫。
/// </summary>
public static class CsvTables
{
    /// <summary>SystemMetrics。</summary>
    public static readonly CsvTable SystemMetrics = new(
        "SystemMetrics",
        1,
        ["SampleId", "TimestampUtc", "TimestampLocal", "ComputerName", "CpuUsagePercent", "CpuStatus",
         "MemoryTotalGB", "MemoryUsedGB", "MemoryFreeGB", "MemoryUsagePercent", "MemoryStatus",
         "DiskStatus", "DiskIoStatus", "GpuStatus", "NetworkStatus", "ProcessStatus", "TemperatureStatus",
         "SampleDurationMs", "LoopOverrun"],
        BuildSystemRows);

    /// <summary>DiskMetrics（磁碟容量）。</summary>
    public static readonly CsvTable DiskMetrics = new(
        "DiskMetrics",
        1,
        ["SampleId", "TimestampUtc", "Drive", "VolumeLabel", "TotalGB", "UsedGB", "FreeGB", "UsedPercent", "FreePercent", "Status", "ErrorCode"],
        s => DeviceRows<DiskResult, DiskItem>(s, ModuleNames.Disk, 7, r => r.Disks, d =>
            [Escape(d.Drive), Escape(d.VolumeLabel), Number(d.TotalGB), Number(d.UsedGB), Number(d.FreeGB), Number(d.UsedPercent), Number(d.FreePercent)]));

    /// <summary>DiskIoMetrics（磁碟 I/O）。</summary>
    public static readonly CsvTable DiskIoMetrics = new(
        "DiskIoMetrics",
        1,
        ["SampleId", "TimestampUtc", "Scope", "Disk", "ReadMBps", "WriteMBps", "ReadIOPS", "WriteIOPS", "ReadLatencyMs", "WriteLatencyMs", "BusyPercent", "Status", "ErrorCode"],
        s => DeviceRows<DiskIoResult, DiskIoItem>(s, ModuleNames.DiskIo, 9, r => r.Disks, d =>
            [d.Scope.ToString(), Escape(d.Disk), Number(d.ReadMBps), Number(d.WriteMBps), Number(d.ReadIops), Number(d.WriteIops), Number(d.ReadLatencyMs), Number(d.WriteLatencyMs), Number(d.BusyPercent)]));

    /// <summary>GpuMetrics。</summary>
    public static readonly CsvTable GpuMetrics = new(
        "GpuMetrics",
        1,
        ["SampleId", "TimestampUtc", "GpuIndex", "GpuName", "UsagePercent", "MemoryUsedMB", "MemoryTotalMB", "Status", "ErrorCode"],
        s => DeviceRows<GpuResult, GpuItem>(s, ModuleNames.Gpu, 5, r => r.Gpus, g =>
            [WholeNumber(g.GpuIndex), Escape(g.GpuName), Number(g.UsagePercent), Number(g.MemoryUsedMB), Number(g.MemoryTotalMB)]));

    /// <summary>NetworkMetrics。</summary>
    public static readonly CsvTable NetworkMetrics = new(
        "NetworkMetrics",
        1,
        ["SampleId", "TimestampUtc", "AdapterName", "InterfaceDescription", "InterfaceId", "IsPhysical", "ReceivedBytes", "SentBytes", "DownloadMbps", "UploadMbps", "Status", "ErrorCode"],
        s => DeviceRows<NetworkResult, NetworkAdapterItem>(s, ModuleNames.Network, 8, r => r.Adapters, a =>
            [Escape(a.AdapterName), Escape(a.InterfaceDescription), Escape(a.InterfaceId), Boolean(a.IsPhysical), WholeNumber(a.ReceivedBytes), WholeNumber(a.SentBytes), Number(a.DownloadMbps), Number(a.UploadMbps)]));

    /// <summary>ProcessMetrics（只在程序紀錄到期時寫入）。</summary>
    public static readonly CsvTable ProcessMetrics = new(
        "ProcessMetrics",
        1,
        ["SampleId", "TimestampUtc", "Rank", "ProcessName", "Pid", "WorkingSetMB", "StartTimeUtc", "Status"],
        BuildProcessRows);

    /// <summary>TemperatureMetrics：GPU（來自 GPU 模組）與 ACPI 熱區。</summary>
    public static readonly CsvTable TemperatureMetrics = new(
        "TemperatureMetrics",
        1,
        ["SampleId", "TimestampUtc", "SensorType", "SensorName", "TemperatureC", "Status", "ErrorCode"],
        BuildTemperatureRows);

    /// <summary>所有 CSV，依寫入順序。</summary>
    public static IReadOnlyList<CsvTable> All { get; } =
        [SystemMetrics, DiskMetrics, DiskIoMetrics, GpuMetrics, NetworkMetrics, ProcessMetrics, TemperatureMetrics];

    private static IEnumerable<string[]> BuildSystemRows(MonitorSnapshot s)
    {
        var cpu = s.Find(ModuleNames.Cpu) as CpuResult;
        var memory = s.Find(ModuleNames.Memory) as MemoryResult;
        yield return
        [
            s.SampleId.ToString(), Utc(s.TimestampUtc), Local(s.TimestampLocal), Escape(s.ComputerName),
            Number(cpu?.UsagePercent), Status(s.Find(ModuleNames.Cpu)?.Status),
            Number(memory?.TotalGB), Number(memory?.UsedGB), Number(memory?.FreeGB), Number(memory?.UsagePercent),
            Status(s.Find(ModuleNames.Memory)?.Status),
            Status(s.Find(ModuleNames.Disk)?.Status), Status(s.Find(ModuleNames.DiskIo)?.Status),
            Status(s.Find(ModuleNames.Gpu)?.Status), Status(s.Find(ModuleNames.Network)?.Status),
            Status(s.Find(ModuleNames.Process)?.Status), Status(s.Find(ModuleNames.Temperature)?.Status),
            Number(s.SampleDuration.TotalMilliseconds), Boolean(s.LoopOverrun),
        ];
    }

    private static IEnumerable<string[]> DeviceRows<TResult, TItem>(
        MonitorSnapshot s, string module, int itemFieldCount, Func<TResult, IReadOnlyList<TItem>> items, Func<TItem, string[]> fields)
        where TResult : ModuleResult
        where TItem : DeviceItem
    {
        ModuleResult? result = s.Find(module);
        if (result is null)
        {
            yield break;
        }

        IReadOnlyList<TItem> list = result is TResult typed ? items(typed) : [];
        if (list.Count == 0)
        {
            yield return StatusRow(s, result, itemFieldCount);
            yield break;
        }

        foreach (TItem item in list)
        {
            yield return [s.SampleId.ToString(), Utc(s.TimestampUtc), .. fields(item), Status(item.Status), Code(item.ErrorCode)];
        }
    }

    private static IEnumerable<string[]> BuildProcessRows(MonitorSnapshot s)
    {
        ModuleResult? result = s.Find(ModuleNames.Process);
        if (result is null)
        {
            yield break;
        }

        if (result is not ProcessResult processes || processes.Processes.Count == 0)
        {
            yield return [s.SampleId.ToString(), Utc(s.TimestampUtc), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, Status(result.Status)];
            yield break;
        }

        foreach (ProcessItem p in processes.Processes)
        {
            yield return
            [
                s.SampleId.ToString(), Utc(s.TimestampUtc), WholeNumber(p.Rank), Escape(p.ProcessName), WholeNumber(p.Pid),
                Number(p.WorkingSetMB), Utc(p.StartTimeUtc), Status(result.Status),
            ];
        }
    }

    private static IEnumerable<string[]> BuildTemperatureRows(MonitorSnapshot s)
    {
        if (s.Find(ModuleNames.Gpu) is GpuResult gpu)
        {
            foreach (GpuItem g in gpu.Gpus)
            {
                yield return [s.SampleId.ToString(), Utc(s.TimestampUtc), "Gpu", Escape($"{g.DeviceId} {g.GpuName}"), Number(g.TemperatureC), Status(g.Status), Code(g.ErrorCode)];
            }
        }

        ModuleResult? zones = s.Find(ModuleNames.Temperature);
        if (zones is null)
        {
            yield break;
        }

        IReadOnlyList<ThermalZoneItem> sensors = zones is TemperatureResult t ? t.Sensors : [];
        if (sensors.Count == 0)
        {
            yield return [s.SampleId.ToString(), Utc(s.TimestampUtc), "AcpiThermalZone", string.Empty, string.Empty, Status(zones.Status), Code(zones.ErrorCode)];
            yield break;
        }

        foreach (ThermalZoneItem z in sensors)
        {
            yield return [s.SampleId.ToString(), Utc(s.TimestampUtc), "AcpiThermalZone", Escape(z.SensorName), Number(z.TemperatureC), Status(z.Status), Code(z.ErrorCode)];
        }
    }

    private static string[] StatusRow(MonitorSnapshot s, ModuleResult result, int columnCount)
    {
        // 狀態列：SampleId、TimestampUtc、（項目欄位留空）、Status、ErrorCode。
        string[] row = [s.SampleId.ToString(), Utc(s.TimestampUtc), .. Enumerable.Repeat(string.Empty, columnCount), Status(result.Status), Code(result.ErrorCode)];
        return row;
    }
}
