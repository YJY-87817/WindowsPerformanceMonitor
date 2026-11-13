using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class ThermalZoneMonitorTests : IDisposable
{
    private readonly FakePdhFactory _pdh = new();
    private readonly ThermalZoneMonitor _monitor;

    public ThermalZoneMonitorTests() => _monitor = new ThermalZoneMonitor(new AppSettings(), _pdh);

    public void Dispose() => _monitor.Dispose();

    private static PdhArrayValue Array(params (string Name, PdhValue Value)[] items) =>
        PdhArrayValue.Ok(items.ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase));

    private static PdhValue Ok(double value) => new(PdhReadStatus.Ok, value, 0);

    [Fact]
    public void PrefersHighPrecisionTenthsOfKelvin()
    {
        TemperatureResult result = ThermalZoneMonitor.Calculate(
            Array((@"\_TZ.THM0", Ok(3011))), // 301.1 K
            Array((@"\_TZ.THM0", Ok(301))));

        ThermalZoneItem z = Assert.Single(result.Sensors);
        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(@"\_TZ.THM0", z.SensorName);
        Assert.Equal(27.95, z.TemperatureC);
    }

    [Fact]
    public void FallsBackToKelvinWhenHighPrecisionMissing()
    {
        TemperatureResult withoutCounter = ThermalZoneMonitor.Calculate(null, Array(("TZ0", Ok(300))));
        TemperatureResult notReported = ThermalZoneMonitor.Calculate(Array(), Array(("TZ0", Ok(300))));
        TemperatureResult hpFailed = ThermalZoneMonitor.Calculate(
            Array(("TZ0", new PdhValue(PdhReadStatus.Failed, 0, 0xC0000BB9))), Array(("TZ0", Ok(300))));

        Assert.Equal(26.85, Assert.Single(withoutCounter.Sensors).TemperatureC);
        Assert.Equal(26.85, Assert.Single(notReported.Sensors).TemperatureC);
        Assert.Equal(MonitorStatus.OK, Assert.Single(hpFailed.Sensors).Status);
    }

    [Fact]
    public void SortsZonesAndKeepsEachIndependent()
    {
        TemperatureResult result = ThermalZoneMonitor.Calculate(
            null,
            Array(("TZ1", Ok(320)), ("TZ0", new PdhValue(PdhReadStatus.Failed, 0, 0xC0000BB9))));

        Assert.Equal(["TZ0", "TZ1"], result.Sensors.Select(s => s.SensorName));
        Assert.Equal(MonitorStatus.Error, result.Sensors[0].Status);
        Assert.Equal(ErrorCode.DataError, result.Sensors[0].ErrorCode);
        Assert.Contains("0xC0000BB9", result.Sensors[0].ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(MonitorStatus.OK, result.Sensors[1].Status);
        Assert.Equal(MonitorStatus.Error, result.Status);
    }

    [Theory]
    [InlineData(0)] // 0 K → −273°C
    [InlineData(230)] // −43°C
    [InlineData(430)] // 157°C
    [InlineData(double.NaN)]
    public void UnreasonableValuesAreError(double kelvin)
    {
        ThermalZoneItem z = Assert.Single(ThermalZoneMonitor.Calculate(null, Array(("TZ0", Ok(kelvin)))).Sensors);

        Assert.Equal(MonitorStatus.Error, z.Status);
        Assert.Equal(ErrorCode.DataError, z.ErrorCode);
        Assert.Null(z.TemperatureC);
    }

    [Fact]
    public void NotReadyIsWarmingUp()
    {
        ThermalZoneItem z = Assert.Single(ThermalZoneMonitor.Calculate(null, Array(("TZ0", new PdhValue(PdhReadStatus.NotReady, 0, 0)))).Sensors);

        Assert.Equal(MonitorStatus.WarmingUp, z.Status);
    }

    [Fact]
    public void NoZonesIsUnavailable()
    {
        TemperatureResult result = ThermalZoneMonitor.Calculate(Array(), Array());

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
    }

    [Fact]
    public void NoDataWithoutZonesIsUnavailableNotWarmingUp()
    {
        // Windows CI 的 VM：有 Thermal Zone Information 物件但沒有熱區，PdhGetFormattedCounterArray 回傳無資料。
        TemperatureResult result = ThermalZoneMonitor.Calculate(
            PdhArrayValue.Empty(PdhReadStatus.NotReady, 0x800007D5), PdhArrayValue.Empty(PdhReadStatus.NotReady, 0x800007D5));

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
        Assert.Contains("0x800007D5", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedArrayWithoutZonesIsError()
    {
        TemperatureResult result = ThermalZoneMonitor.Calculate(null, PdhArrayValue.Empty(PdhReadStatus.Failed, 0xC0000BB9));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.DataError, result.ErrorCode);
    }

    [Fact]
    public async Task AddsBothCountersAndReadsZones()
    {
        _pdh.ArrayValues = path => path == ThermalZoneMonitor.HighPrecisionPath ? Array(("TZ0", Ok(3100))) : Array(("TZ0", Ok(310)));

        await _monitor.WarmUpAsync(CancellationToken.None);
        var result = Assert.IsType<TemperatureResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal([ThermalZoneMonitor.TemperaturePath, ThermalZoneMonitor.HighPrecisionPath], _pdh.AddedPaths);
        Assert.Equal(36.85, Assert.Single(result.Sensors).TemperatureC);
        Assert.Equal(1, _pdh.Created);
    }

    [Fact]
    public async Task WorksWithoutHighPrecisionCounter()
    {
        _pdh.AddArrayThrowsFor = ThermalZoneMonitor.HighPrecisionPath;
        _pdh.ArrayValues = _ => Array(("TZ0", Ok(310)));

        var result = Assert.IsType<TemperatureResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(36.85, Assert.Single(result.Sensors).TemperatureC);
        Assert.Equal(0, _pdh.Disposed);
    }

    [Fact]
    public async Task MissingThermalZoneObjectIsUnavailableAndRetried()
    {
        _pdh.AddArrayThrowsFor = ThermalZoneMonitor.TemperaturePath;

        var first = Assert.IsType<TemperatureResult>(await _monitor.SampleAsync(CancellationToken.None));
        var second = Assert.IsType<TemperatureResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Unavailable, first.Status);
        Assert.Equal(ErrorCode.Unavailable, first.ErrorCode);
        Assert.Contains("Thermal Zone Information", first.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(MonitorStatus.Unavailable, second.Status);
        Assert.Equal(2, _pdh.Created);
        Assert.Equal(2, _pdh.Disposed);
    }

    [Fact]
    public async Task PdhFailureRecreatesQuery()
    {
        await _monitor.WarmUpAsync(CancellationToken.None);
        _pdh.CollectThrows = new PdhException("PdhCollectQueryData", 0xC0000BB9);

        var failed = Assert.IsType<TemperatureResult>(await _monitor.SampleAsync(CancellationToken.None));
        await _monitor.SampleAsync(CancellationToken.None);

        Assert.Equal(MonitorStatus.Error, failed.Status);
        Assert.Equal(ErrorCode.TransientError, failed.ErrorCode);
        Assert.Equal(2, _pdh.Created);
    }

    [Fact]
    public async Task MissingPdhDllIsUnavailable()
    {
        _pdh.CreateThrows = new DllNotFoundException("pdh.dll");

        var result = Assert.IsType<TemperatureResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
    }

    [Fact]
    public void FollowsSettingsAndSaysNotCpuTemperature()
    {
        var disabled = new ThermalZoneMonitor(new AppSettings { Temperature = new ModuleSettings { Enabled = false } }, _pdh);

        Assert.False(disabled.IsEnabled);
        Assert.Equal(ModuleNames.Temperature, _monitor.Name);
        Assert.Contains("不是 CPU 核心溫度", _monitor.SourceDescription, StringComparison.Ordinal);
    }
}
