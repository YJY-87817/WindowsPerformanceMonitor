using System.Text.Json;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Application.Output;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Application.Tests;

public class SnapshotFormatterTests
{
    private static readonly MonitorSnapshot Snapshot = new(
        new SampleId(Guid.Parse("0123456789abcdef0123456789abcdef")),
        new DateTimeOffset(2026, 11, 13, 6, 0, 0, TimeSpan.Zero),
        TimeSpan.FromHours(8),
        "PC",
        [
            new CpuResult(MonitorStatus.OK, 25.55) { Duration = TimeSpan.FromMilliseconds(12) },
            new MemoryResult(MonitorStatus.OK, 16, 10.4321, 5.5679, 65.2),
            new DiskResult(MonitorStatus.Error, [
                new DiskItem(MonitorStatus.OK, "C:", null, 100, 72.5, 27.5, 72.5, 27.5),
                new DiskItem(MonitorStatus.Error, "D:", null, null, null, null, null, null),
            ]),
            ModuleResult.Unavailable(ModuleNames.Gpu, "找不到 nvidia-smi"),
            new ProcessResult(MonitorStatus.OK, [new ProcessItem(1, "chrome", 1, 1200.55, null), new ProcessItem(2, "code", 2, 850.2, null), new ProcessItem(3, "a", 3, 3, null), new ProcessItem(4, "b", 4, 1, null)], 0, 0),
            new DiskIoResult(MonitorStatus.OK, [
                new DiskIoItem(MonitorStatus.OK, DiskIoScope.Logical, "C:", 12.345, 1.5, 100, 20, 0.8, 1.2, 37.25),
                new DiskIoItem(MonitorStatus.WarmingUp, DiskIoScope.Physical, "0 C: D:", null, null, null, null, null, null, null),
            ]),
        ],
        TimeSpan.FromMilliseconds(345.6),
        LoopOverrun: false);

    private static StorageStatus Storage()
    {
        var storage = new StorageStatus();
        storage.AddWritten(3);
        return storage;
    }

    [Fact]
    public void Text_ListsModulesWithStatusValueAndError()
    {
        string text = SnapshotFormatter.ToText(Snapshot, Storage());

        Assert.Contains("Time: 2026-11-13T14:00:00+08:00  Computer: PC", text, StringComparison.Ordinal);
        Assert.Contains("SampleId: 0123456789abcdef0123456789abcdef", text, StringComparison.Ordinal);
        Assert.Contains("Cpu          OK           25.6%", text, StringComparison.Ordinal);
        Assert.Contains("Memory       OK           65.2% (10.43 / 16.00 GB)", text, StringComparison.Ordinal);
        Assert.Contains("Disk         Error        C: 72.5% used, 27.50 GB free; D: Error", text, StringComparison.Ordinal);
        Assert.Contains("Gpu          Unavailable  [Unavailable] 找不到 nvidia-smi", text, StringComparison.Ordinal);
        Assert.Contains("Process      OK           chrome 1200.6 MB, code 850.2 MB, a 3.0 MB", text, StringComparison.Ordinal);
        Assert.Contains("DiskIo       OK           C: R 12.35 / W 1.50 MB/s, busy 37.3%; PhysicalDisk(0 C: D:) WarmingUp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("b 1.0 MB", text, StringComparison.Ordinal);
        Assert.Contains("Duration: 346 ms  LoopOverrun: False", text, StringComparison.Ordinal);
        Assert.Contains("CSV: written 3, lost 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_ContainsFullModuleResultsWithEnumNames()
    {
        string json = SnapshotFormatter.ToJson(Snapshot, Storage());

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal("0123456789abcdef0123456789abcdef", root.GetProperty("sampleId").GetString());
        Assert.Equal(345.6, root.GetProperty("sampleDurationMs").GetDouble());
        JsonElement cpu = root.GetProperty("modules")[0];
        Assert.Equal("Cpu", cpu.GetProperty("module").GetString());
        Assert.Equal("OK", cpu.GetProperty("status").GetString());
        Assert.Equal(25.55, cpu.GetProperty("usagePercent").GetDouble());
        JsonElement disk = root.GetProperty("modules")[2];
        Assert.Equal(2, disk.GetProperty("disks").GetArrayLength());
        Assert.False(disk.TryGetProperty("devices", out _));
        JsonElement gpu = root.GetProperty("modules")[3];
        Assert.Equal("Unavailable", gpu.GetProperty("errorCode").GetString());
        JsonElement io = root.GetProperty("modules")[5].GetProperty("disks")[1];
        Assert.Equal("Physical", io.GetProperty("scope").GetString());
        Assert.Equal("0 C: D:", io.GetProperty("disk").GetString());
        Assert.Equal(3, root.GetProperty("csv").GetProperty("writtenRows").GetInt64());
        Assert.Contains("找不到 nvidia-smi", json, StringComparison.Ordinal);
    }
}
