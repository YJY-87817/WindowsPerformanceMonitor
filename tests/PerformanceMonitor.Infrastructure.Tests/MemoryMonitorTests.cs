using System.ComponentModel;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Memory;

namespace PerformanceMonitor.Infrastructure.Tests;

public class MemoryMonitorTests
{
    private const ulong GB = 1_073_741_824;

    private sealed class FakeSource(Func<MemoryReading> read) : IMemoryStatusSource
    {
        public MemoryReading Read() => read();
    }

    private static async Task<MemoryResult> SampleAsync(Func<MemoryReading> read) =>
        Assert.IsType<MemoryResult>(await new MemoryMonitor(new AppSettings(), new FakeSource(read)).SampleAsync(CancellationToken.None));

    [Fact]
    public void Calculate_UsesBinaryGigabytesAndAvailableMemory()
    {
        MemoryResult result = MemoryMonitor.Calculate(new MemoryReading(16 * GB, 4 * GB));

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(16, result.TotalGB);
        Assert.Equal(12, result.UsedGB);
        Assert.Equal(4, result.FreeGB);
        Assert.Equal(75, result.UsagePercent);
    }

    [Fact]
    public void Calculate_ZeroTotalIsDataErrorWithoutDividing()
    {
        MemoryResult result = MemoryMonitor.Calculate(new MemoryReading(0, 0));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.DataError, result.ErrorCode);
        Assert.Null(result.UsagePercent);
    }

    [Fact]
    public void Calculate_AvailableGreaterThanTotalIsDataError()
    {
        Assert.Equal(ErrorCode.DataError, MemoryMonitor.Calculate(new MemoryReading(4 * GB, 5 * GB)).ErrorCode);
    }

    [Fact]
    public void Calculate_AllMemoryAvailableIsZeroPercentNotMissing()
    {
        MemoryResult result = MemoryMonitor.Calculate(new MemoryReading(8 * GB, 8 * GB));

        Assert.Equal(0, result.UsagePercent);
        Assert.Equal(0, result.UsedGB);
    }

    [Fact]
    public async Task Win32FailureIsTransientError()
    {
        MemoryResult result = await SampleAsync(() => throw new Win32Exception(5));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.TransientError, result.ErrorCode);
        Assert.StartsWith("GlobalMemoryStatusEx 失敗", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingKernel32IsUnavailable()
    {
        MemoryResult result = await SampleAsync(() => throw new DllNotFoundException());

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
    }

    [Fact]
    public void NameEnabledAndSource()
    {
        var monitor = new MemoryMonitor(new AppSettings { Memory = new ModuleSettings { Enabled = false } }, new FakeSource(() => default));

        Assert.Equal(ModuleNames.Memory, monitor.Name);
        Assert.False(monitor.IsEnabled);
        Assert.Contains("GlobalMemoryStatusEx", monitor.SourceDescription, StringComparison.Ordinal);
    }
}
