using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure.Tests;

/// <summary>以佇列提供讀值的假 PDH 查詢。</summary>
internal sealed class FakePdhFactory : IPdhQueryFactory
{
    public Queue<PdhValue> Values { get; } = new();

    public List<string> AddedPaths { get; } = [];

    public int Created { get; private set; }

    public int Disposed { get; private set; }

    public int Collects { get; private set; }

    public Exception? CollectThrows { get; set; }

    public Exception? CreateThrows { get; set; }

    public IPdhQuery Create()
    {
        if (CreateThrows is not null)
        {
            throw CreateThrows;
        }

        Created++;
        return new Query(this);
    }

    private sealed class Query(FakePdhFactory owner) : IPdhQuery
    {
        public IPdhCounter AddCounter(string englishPath)
        {
            owner.AddedPaths.Add(englishPath);
            return new Counter(owner);
        }

        public void Collect()
        {
            owner.Collects++;
            if (owner.CollectThrows is Exception ex)
            {
                owner.CollectThrows = null;
                throw ex;
            }
        }

        public void Dispose() => owner.Disposed++;
    }

    private sealed class Counter(FakePdhFactory owner) : IPdhCounter
    {
        public PdhValue Read() => owner.Values.Count > 0 ? owner.Values.Dequeue() : new PdhValue(PdhReadStatus.NotReady, 0, 0);
    }
}

public sealed class CpuMonitorTests : IDisposable
{
    private readonly FakePdhFactory _pdh = new();
    private readonly CpuMonitor _monitor;

    public CpuMonitorTests() => _monitor = new CpuMonitor(new AppSettings(), _pdh);

    public void Dispose() => _monitor.Dispose();

    private async Task<CpuResult> SampleAsync() => Assert.IsType<CpuResult>(await _monitor.SampleAsync(CancellationToken.None));

    [Fact]
    public async Task UsesEnglishProcessorTotalCounterAndWarmsUp()
    {
        await _monitor.WarmUpAsync(CancellationToken.None);

        Assert.Equal([@"\Processor(_Total)\% Processor Time"], _pdh.AddedPaths);
        Assert.Equal(1, _pdh.Collects);
        Assert.Contains("PDH", _monitor.SourceDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstReadingWithoutBaselineIsWarmingUp()
    {
        CpuResult result = await SampleAsync();

        Assert.Equal(MonitorStatus.WarmingUp, result.Status);
        Assert.Null(result.UsagePercent);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(25.5)]
    [InlineData(100.0)]
    public async Task ValidValueIsOk(double usage)
    {
        _pdh.Values.Enqueue(new PdhValue(PdhReadStatus.Ok, usage, 0));

        CpuResult result = await SampleAsync();

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(usage, result.UsagePercent);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public async Task OutOfRangeIsDataError(double usage)
    {
        _pdh.Values.Enqueue(new PdhValue(PdhReadStatus.Ok, usage, 0));

        CpuResult result = await SampleAsync();

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.DataError, result.ErrorCode);
        Assert.Null(result.UsagePercent);
    }

    [Fact]
    public async Task FailedReadIsDataErrorWithPdhCode()
    {
        _pdh.Values.Enqueue(new PdhValue(PdhReadStatus.Failed, 0, 0xC0000BB9));

        CpuResult result = await SampleAsync();

        Assert.Equal(ErrorCode.DataError, result.ErrorCode);
        Assert.Contains("0xC0000BB9", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PdhExceptionIsTransientErrorAndQueryIsRebuiltNextTime()
    {
        await SampleAsync();
        _pdh.CollectThrows = new PdhException("PdhCollectQueryData", 0xC0000BBC);

        CpuResult failed = await SampleAsync();
        CpuResult next = await SampleAsync();

        Assert.Equal(MonitorStatus.Error, failed.Status);
        Assert.Equal(ErrorCode.TransientError, failed.ErrorCode);
        Assert.Equal(1, _pdh.Disposed);
        Assert.Equal(2, _pdh.Created);
        Assert.Equal(MonitorStatus.WarmingUp, next.Status);
    }

    [Fact]
    public async Task MissingPdhLibraryIsUnavailable()
    {
        _pdh.CreateThrows = new DllNotFoundException("pdh.dll");

        await _monitor.WarmUpAsync(CancellationToken.None);
        CpuResult result = await SampleAsync();

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
    }

    [Fact]
    public void EnabledFollowsSettings()
    {
        using var disabled = new CpuMonitor(new AppSettings { Cpu = new ModuleSettings { Enabled = false } }, _pdh);

        Assert.False(disabled.IsEnabled);
        Assert.True(_monitor.IsEnabled);
        Assert.Equal(ModuleNames.Cpu, _monitor.Name);
    }
}
