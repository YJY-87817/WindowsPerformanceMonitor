using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

public class SettingsValidatorTests
{
    private static readonly AppSettings Defaults = new();

    private static string Single(AppSettings settings) => Assert.Single(SettingsValidator.Validate(settings));

    [Fact]
    public void DefaultsAreValid()
    {
        Assert.Empty(SettingsValidator.Validate(Defaults));
    }

    [Theory]
    [InlineData(0, 2, 10, "monitor.refreshSeconds")]
    [InlineData(2, -1, 10, "monitor.sampleSeconds")]
    [InlineData(2, 2, 0, "monitor.logSeconds")]
    public void IntervalsMustBePositive(int refresh, int sample, int log, string field)
    {
        string error = Single(Defaults with { Monitor = new MonitorSettings { RefreshSeconds = refresh, SampleSeconds = sample, LogSeconds = log } });

        Assert.StartsWith(field, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void TopCountMustBeBetween1And20(int topCount)
    {
        Assert.StartsWith("process.topCount", Single(Defaults with { Process = new ProcessSettings { TopCount = topCount } }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void GpuTimeoutMustBeBetween1And30(int seconds)
    {
        Assert.StartsWith("timeouts.gpuSeconds", Single(Defaults with { Timeouts = new TimeoutSettings { GpuSeconds = seconds } }), StringComparison.Ordinal);
    }

    [Fact]
    public void AdapterNamesMustNotBeBlank()
    {
        string error = Single(Defaults with { Network = new NetworkSettings { ExcludeNames = ["ok", " "] } });

        Assert.StartsWith("network.excludeNames", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.5)]
    public void PercentagesMustBeBetween0And100(double value)
    {
        IReadOnlyList<string> errors = SettingsValidator.Validate(
            Defaults with { Thresholds = new ThresholdSettings { CpuPercent = value, CpuRecoveryPercent = -5 } });

        Assert.Contains(errors, e => e.StartsWith("thresholds.cpuPercent", StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryValuesMustFormHysteresis()
    {
        IReadOnlyList<string> errors = SettingsValidator.Validate(Defaults with
        {
            Thresholds = new ThresholdSettings
            {
                CpuPercent = 90,
                CpuRecoveryPercent = 90,
                RamPercent = 80,
                RamRecoveryPercent = 85,
                DiskFreePercent = 10,
                DiskRecoveryFreePercent = 10,
            },
        });

        Assert.Equal(3, errors.Count);
    }

    [Theory]
    [InlineData(5.0, null, "必須同時設定")]
    [InlineData(-1.0, 2.0, "不可小於 0")]
    [InlineData(5.0, 5.0, "必須大於 diskFreeGB")]
    public void DiskGigabyteThresholdRules(double? free, double? recovery, string expected)
    {
        string error = Single(Defaults with { Thresholds = new ThresholdSettings { DiskFreeGB = free, DiskRecoveryFreeGB = recovery } });

        Assert.Contains(expected, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(86_401)]
    public void AlertDurationsMustBeWithinOneDay(int seconds)
    {
        Assert.StartsWith("alert.cooldownSeconds", Single(Defaults with { Alert = new AlertSettings { CooldownSeconds = seconds } }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Data|Bad")]
    [InlineData("Data?")]
    [InlineData("Da:ta")]
    public void DirectoriesMustBeValidPaths(string path)
    {
        Assert.StartsWith("logging.dataDirectory", Single(Defaults with { Logging = new LoggingSettings { DataDirectory = path } }), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"D:\Monitor\Data")]
    [InlineData(@"..\Shared\Data")]
    [InlineData("Data")]
    public void DirectoriesAcceptDriveAndRelativePaths(string path)
    {
        Assert.Empty(SettingsValidator.Validate(Defaults with { Logging = new LoggingSettings { DataDirectory = path } }));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void LockedFileRetryCountMustBeBetween0And5(int count)
    {
        Assert.StartsWith("logging.lockedFileRetryCount", Single(Defaults with { Logging = new LoggingSettings { LockedFileRetryCount = count } }), StringComparison.Ordinal);
    }
}
