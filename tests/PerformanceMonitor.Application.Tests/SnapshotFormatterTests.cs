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
            ModuleResult.Unavailable(ModuleNames.Gpu, "找不到 nvidia-smi"),
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
        Assert.Contains("Gpu          Unavailable  [Unavailable] 找不到 nvidia-smi", text, StringComparison.Ordinal);
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
        JsonElement gpu = root.GetProperty("modules")[2];
        Assert.Equal("Unavailable", gpu.GetProperty("errorCode").GetString());
        Assert.Equal(3, root.GetProperty("csv").GetProperty("writtenRows").GetInt64());
        Assert.Contains("找不到 nvidia-smi", json, StringComparison.Ordinal);
    }
}
