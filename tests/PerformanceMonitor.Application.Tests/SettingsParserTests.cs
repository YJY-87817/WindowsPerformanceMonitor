using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

public class SettingsParserTests
{
    private static SettingsLoadResult Parse(string json) => SettingsParser.Parse(json, "settings.json");

    [Fact]
    public void EmptyObject_UsesDefaultsAndListsDefaultedSections()
    {
        SettingsLoadResult result = Parse("{}");

        Assert.True(result.IsValid);
        Assert.Equal(new AppSettings(), result.Settings with { Network = new AppSettings().Network });
        Assert.Contains("monitor", result.DefaultedFields);
        Assert.Contains("logging", result.DefaultedFields);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ReadsAllSupportedFields()
    {
        const string json = """
            {
              // 允許註解與結尾逗號
              "monitor": { "refreshSeconds": 1, "sampleSeconds": 3, "logSeconds": 30 },
              "cpu": { "enabled": false },
              "diskIo": { "enabled": false },
              "temperature": { "enabled": false },
              "network": { "physicalOnlyByDefault": false, "includeNames": ["Ethernet*"], "excludeNames": ["vEthernet*"] },
              "process": { "topCount": 10, "loggingEnabled": false, "logIntervalSeconds": 60 },
              "thresholds": { "cpuPercent": 95.5, "cpuRecoveryPercent": 80, "diskFreeGB": 5, "diskRecoveryFreeGB": 6, "diskConditionMode": "ALL" },
              "alert": { "enabled": false, "durationSeconds": 0, "cooldownSeconds": 300, "recoveryDurationSeconds": 30, "repeatNotificationAfterCooldown": false },
              "timeouts": { "gpuSeconds": 5 },
              "logging": { "enabled": false, "dataDirectory": "D:\\Monitor\\Data", "logDirectory": "MyLogs", "lockedFileRetryCount": 3 },
            }
            """;

        SettingsLoadResult result = Parse(json);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        AppSettings s = result.Settings;
        Assert.Equal(new MonitorSettings { RefreshSeconds = 1, SampleSeconds = 3, LogSeconds = 30 }, s.Monitor);
        Assert.False(s.Cpu.Enabled);
        Assert.False(s.DiskIo.Enabled);
        Assert.False(s.Temperature.Enabled);
        Assert.True(s.Memory.Enabled);
        Assert.Equal(["Ethernet*"], s.Network.IncludeNames);
        Assert.Equal(["vEthernet*"], s.Network.ExcludeNames);
        Assert.False(s.Network.PhysicalOnlyByDefault);
        Assert.Equal(10, s.Process.TopCount);
        Assert.Equal(95.5, s.Thresholds.CpuPercent);
        Assert.Equal(5, s.Thresholds.DiskFreeGB);
        Assert.Equal(DiskConditionMode.All, s.Thresholds.DiskConditionMode);
        Assert.Equal(300, s.Alert.CooldownSeconds);
        Assert.Equal(5, s.Timeouts.GpuSeconds);
        Assert.Equal(@"D:\Monitor\Data", s.Logging.DataDirectory);
        Assert.Equal(3, s.Logging.LockedFileRetryCount);
    }

    [Fact]
    public void FieldNamesAreCaseInsensitive()
    {
        SettingsLoadResult result = Parse("""{ "Monitor": { "SampleSeconds": 5 } }""");

        Assert.Equal(5, result.Settings.Monitor.SampleSeconds);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void MissingFieldInsidePresentSectionIsListedAsDefaulted()
    {
        SettingsLoadResult result = Parse("""{ "monitor": { "sampleSeconds": 2 } }""");

        Assert.Contains("monitor.logSeconds", result.DefaultedFields);
        Assert.DoesNotContain("monitor.sampleSeconds", result.DefaultedFields);
    }

    [Fact]
    public void InvalidJson_IsAnErrorWithLineNumber()
    {
        SettingsLoadResult result = Parse("{\n  \"monitor\": { \n}");

        Assert.False(result.IsValid);
        Assert.Contains("不是有效的 JSON", result.Errors[0], StringComparison.Ordinal);
        Assert.Contains("第 ", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TypeErrors_AreAllReportedAtOnce()
    {
        const string json = """
            {
              "monitor": { "sampleSeconds": "2", "logSeconds": 2.5 },
              "cpu": { "enabled": "yes" },
              "network": { "includeNames": ["ok", 1] },
              "thresholds": { "diskConditionMode": "none" },
              "logging": "Data"
            }
            """;

        SettingsLoadResult result = Parse(json);

        Assert.False(result.IsValid);
        Assert.Equal(6, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.StartsWith("monitor.sampleSeconds：必須是整數", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("monitor.logSeconds：必須是整數", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("cpu.enabled：", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("network.includeNames：必須是字串陣列", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("thresholds.diskConditionMode：", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.StartsWith("logging：必須是 JSON 物件", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("1")]
    [InlineData("\"Any,All\"")]
    public void DiskConditionMode_AcceptsOnlyNames(string value)
    {
        SettingsLoadResult result = Parse($$"""{ "thresholds": { "diskConditionMode": {{value}} } }""");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NullableNumbersAcceptNull()
    {
        SettingsLoadResult result = Parse("""{ "thresholds": { "diskFreeGB": null, "diskRecoveryFreeGB": null } }""");

        Assert.True(result.IsValid);
        Assert.Null(result.Settings.Thresholds.DiskFreeGB);
    }

    [Fact]
    public void UnknownFields_AreWarningsNotErrors()
    {
        SettingsLoadResult result = Parse("""{ "monitr": {}, "cpu": { "enabled": true, "counterPath": null } }""");

        Assert.True(result.IsValid);
        Assert.Contains("未知欄位 'monitr'，已忽略（請檢查拼字）。", result.Warnings);
        Assert.Contains("未知欄位 'cpu.counterPath'，已忽略（請檢查拼字）。", result.Warnings);
    }

    [Fact]
    public void RangeErrorsFromValidatorAreIncluded()
    {
        SettingsLoadResult result = Parse("""{ "process": { "topCount": 0 }, "monitor": { "sampleSeconds": 0 } }""");

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Equal(new AppSettings(), result.Settings);
    }

    [Fact]
    public void RootMustBeAnObject()
    {
        Assert.False(Parse("[]").IsValid);
    }

    [Fact]
    public void Missing_UsesDefaultsWithWarning()
    {
        SettingsLoadResult result = SettingsLoadResult.Missing(@"C:\x\settings.json");

        Assert.True(result.IsValid);
        Assert.Equal(SettingsLoadResult.BuiltInDefaults, result.Source);
        Assert.Single(result.Warnings);
    }
}
