using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Core.Tests;

public class ModelTests
{
    private sealed record CpuLikeResult(double? UsagePercent) : ModuleResult("Cpu", MonitorStatus.OK);

    [Fact]
    public void SampleId_FormatsAs32HexCharacters()
    {
        string text = SampleId.New().ToString();

        Assert.Matches("^[0-9a-f]{32}$", text);
    }

    [Fact]
    public void ModuleResult_SanitizesErrorMessage()
    {
        ModuleResult result = ModuleResult.Failed("Gpu", ErrorCode.Timeout, "timed out\nstack trace");

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.Timeout, result.ErrorCode);
        Assert.Equal("timed out", result.ErrorMessage);
    }

    [Fact]
    public void ModuleResult_FactoriesSetStatusAndCode()
    {
        Assert.Equal(MonitorStatus.Disabled, ModuleResult.Disabled("Gpu").Status);
        ModuleResult unavailable = ModuleResult.Unavailable("Gpu", "nvidia-smi not found");
        Assert.Equal(MonitorStatus.Unavailable, unavailable.Status);
        Assert.Equal(ErrorCode.Unavailable, unavailable.ErrorCode);
    }

    [Fact]
    public void ModuleResult_WithKeepsDerivedType()
    {
        ModuleResult result = new CpuLikeResult(12.5) with { Duration = TimeSpan.FromMilliseconds(30) };

        CpuLikeResult cpu = Assert.IsType<CpuLikeResult>(result);
        Assert.Equal(12.5, cpu.UsagePercent);
        Assert.Equal(TimeSpan.FromMilliseconds(30), cpu.Duration);
    }

    [Fact]
    public void Snapshot_ProvidesLocalTimestampAndLookup()
    {
        var utc = new DateTimeOffset(2026, 11, 13, 16, 30, 0, TimeSpan.Zero);
        var snapshot = new MonitorSnapshot(
            SampleId.New(), utc, TimeSpan.FromHours(8), "PC", [new CpuLikeResult(1), ModuleResult.Disabled("Gpu")], TimeSpan.Zero, false);

        Assert.Equal(new DateTimeOffset(2026, 11, 14, 0, 30, 0, TimeSpan.FromHours(8)), snapshot.TimestampLocal);
        Assert.Equal(MonitorStatus.Disabled, snapshot.Find("Gpu")!.Status);
        Assert.Null(snapshot.Find("Network"));
        Assert.NotNull(snapshot.Find<CpuLikeResult>());
    }
}
