using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

public class SettingsSerializerTests
{
    [Fact]
    public void DefaultsRoundTripWithoutWarnings()
    {
        string json = SettingsSerializer.ToJson(new AppSettings());

        SettingsLoadResult result = SettingsParser.Parse(json, "test");

        Assert.True(result.IsValid);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.DefaultedFields);
        Assert.Equal(json, SettingsSerializer.ToJson(result.Settings));
    }

    [Fact]
    public void CustomValuesRoundTrip()
    {
        var settings = new AppSettings
        {
            Monitor = new MonitorSettings { RefreshSeconds = 1, SampleSeconds = 3, LogSeconds = 30 },
            Gpu = new ModuleSettings { Enabled = false },
            Network = new NetworkSettings { PhysicalOnlyByDefault = false, IncludeNames = ["乙太網路", "Wi-Fi*"], ExcludeNames = ["vEthernet*"] },
            Process = new ProcessSettings { TopCount = 12, LoggingEnabled = false, LogIntervalSeconds = 60 },
            Thresholds = new ThresholdSettings { CpuPercent = 95.5, DiskFreeGB = 20, DiskRecoveryFreeGB = 30, DiskConditionMode = DiskConditionMode.All },
            Alert = new AlertSettings { Enabled = false, CooldownSeconds = 0 },
            Timeouts = new TimeoutSettings { GpuSeconds = 10 },
            Logging = new LoggingSettings { Enabled = false, DataDirectory = @"D:\效能\Data", LogDirectory = "Logs", LockedFileRetryCount = 3 },
        };
        string json = SettingsSerializer.ToJson(settings);

        SettingsLoadResult result = SettingsParser.Parse(json, "test");

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(json, SettingsSerializer.ToJson(result.Settings));
        Assert.Contains("\"乙太網路\"", json, StringComparison.Ordinal); // 中文不跳脫
        Assert.Contains(@"""D:\\效能\\Data""", json, StringComparison.Ordinal);
        Assert.Contains("\"diskConditionMode\": \"all\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsetDiskGbIsWrittenAsNull() =>
        Assert.Contains("\"diskFreeGB\": null", SettingsSerializer.ToJson(new AppSettings()), StringComparison.Ordinal);
}
