using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.Presentation.Tests;

public class SettingsViewModelTests
{
    private readonly FakeSettingsService _service = new();
    private readonly FakeRestart _restart = new();

    private SettingsViewModel Create(AppSettings? current = null, bool valid = true) =>
        new(current ?? new AppSettings(), valid, _service, _restart);

    [Fact]
    public void ListsEveryFieldOfSettingsJsonInSections()
    {
        SettingsViewModel vm = Create();
        string[] keys = [.. vm.Sections.SelectMany(s => s.Fields).Select(f => f.Key)];

        // 與 SettingsSerializer 寫出的所有欄位一致（規格 §7.2 全部欄位）
        using var doc = System.Text.Json.JsonDocument.Parse(SettingsSerializer.ToJson(new AppSettings()));
        string[] jsonKeys = [.. doc.RootElement.EnumerateObject().SelectMany(section => section.Value.EnumerateObject().Select(f => $"{section.Name}.{f.Name}"))];
        Assert.Equal(jsonKeys.Order(), keys.Order());
        Assert.Equal(["監控週期", "監控項目", "網路介面卡篩選", "程序", "告警門檻", "告警時間", "資料、紀錄與逾時"], vm.Sections.Select(s => s.Title));
        Assert.Equal("設定", vm.Title);
    }

    [Fact]
    public void LoadsCurrentValues()
    {
        var current = new AppSettings
        {
            Monitor = new MonitorSettings { SampleSeconds = 5 },
            Network = new NetworkSettings { IncludeNames = ["Ethernet", "Wi-Fi*"] },
            Thresholds = new ThresholdSettings { DiskFreeGB = 20, DiskRecoveryFreeGB = 25.5, DiskConditionMode = DiskConditionMode.All },
            Gpu = new ModuleSettings { Enabled = false },
        };

        SettingsViewModel vm = Create(current);

        Assert.Equal("5", vm.Field("monitor.sampleSeconds").Text);
        Assert.Equal("Ethernet, Wi-Fi*", vm.Field("network.includeNames").Text);
        Assert.Equal("20", vm.Field("thresholds.diskFreeGB").Text);
        Assert.Equal("25.5", vm.Field("thresholds.diskRecoveryFreeGB").Text);
        Assert.Equal("all", vm.Field("thresholds.diskConditionMode").Text);
        Assert.False(vm.Field("gpu.enabled").Checked);
        Assert.True(vm.Field("cpu.enabled").IsBoolean);
        Assert.Equal("", vm.Field("network.excludeNames").Text);
        Assert.Equal("1～20（process.topCount）", vm.Field("process.topCount").Hint);
    }

    [Fact]
    public async Task SavesValidChangesAndRestarts()
    {
        SettingsViewModel vm = Create();
        vm.Field("monitor.sampleSeconds").Text = " 3 ";
        vm.Field("network.excludeNames").Text = "vEthernet*,\nVPN ,";
        vm.Field("thresholds.diskFreeGB").Text = "10";
        vm.Field("thresholds.diskRecoveryFreeGB").Text = "15";
        vm.Field("thresholds.diskConditionMode").Text = "all";
        vm.Field("temperature.enabled").Checked = false;
        vm.Field("logging.dataDirectory").Text = @"D:\PerfData ";

        await vm.SaveCommand.ExecuteAsync(null);

        AppSettings saved = Assert.Single(_service.Saved);
        Assert.Equal(3, saved.Monitor.SampleSeconds);
        Assert.Equal(["vEthernet*", "VPN"], saved.Network.ExcludeNames);
        Assert.Equal(10, saved.Thresholds.DiskFreeGB);
        Assert.Equal(DiskConditionMode.All, saved.Thresholds.DiskConditionMode);
        Assert.False(saved.Temperature.Enabled);
        Assert.Equal(@"D:\PerfData", saved.Logging.DataDirectory);
        Assert.Equal(1, _restart.Restarts);
        Assert.False(vm.HasErrors);
        Assert.Equal("已儲存，正在停止監控並重新啟動…", vm.Message);
    }

    [Fact]
    public async Task ParseErrorsBlockSaveAndKeepInput()
    {
        SettingsViewModel vm = Create();
        vm.Field("monitor.sampleSeconds").Text = "兩秒";
        vm.Field("thresholds.cpuPercent").Text = "9O";
        vm.Field("thresholds.diskFreeGB").Text = "abc";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_service.Saved);
        Assert.Equal(0, _restart.Restarts);
        Assert.Equal(
            [
                "monitor.sampleSeconds（取樣週期（秒））必須是整數：「兩秒」",
                "thresholds.cpuPercent（CPU 告警（%））必須是數字：「9O」",
                "thresholds.diskFreeGB（磁碟可用低於（GB））必須是數字或空白：「abc」",
            ],
            vm.Errors);
        Assert.Equal("兩秒", vm.Field("monitor.sampleSeconds").Text);
        Assert.Equal("未儲存：請修正下列問題。", vm.Message);
    }

    [Fact]
    public async Task ValidatorRulesBlockSave()
    {
        SettingsViewModel vm = Create();
        vm.Field("process.topCount").Text = "50";
        vm.Field("thresholds.cpuRecoveryPercent").Text = "95";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_service.Saved);
        Assert.Equal(2, vm.Errors.Count);
        Assert.Contains(vm.Errors, e => e.Contains("process.topCount", StringComparison.Ordinal));
        Assert.Contains(vm.Errors, e => e.Contains("cpuRecoveryPercent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnwritableDirectoryBlocksSave()
    {
        _service.DirectoryErrors = ["Z:\\Data：拒絕存取"];
        SettingsViewModel vm = Create();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_service.Saved);
        Assert.Equal(["資料目錄無法寫入：Z:\\Data：拒絕存取"], vm.Errors);
    }

    [Fact]
    public async Task WriteFailureIsShownAndDoesNotRestart()
    {
        _service.SaveThrows = new UnauthorizedAccessException("拒絕存取");
        SettingsViewModel vm = Create();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["寫入設定檔失敗：拒絕存取"], vm.Errors);
        Assert.Equal(0, _restart.Restarts);
    }

    [Fact]
    public void RevertAndDefaults()
    {
        SettingsViewModel vm = Create(new AppSettings { Process = new ProcessSettings { TopCount = 9 } });
        vm.Field("process.topCount").Text = "12";

        vm.DefaultsCommand.Execute(null);
        Assert.Equal("5", vm.Field("process.topCount").Text);
        Assert.Equal("已填入內建預設值（尚未儲存）。", vm.Message);

        vm.RevertCommand.Execute(null);
        Assert.Equal("9", vm.Field("process.topCount").Text);
        Assert.Empty(_service.Saved);
    }

    [Fact]
    public void BuildWithoutChangesEqualsCurrentAsJson()
    {
        var current = new AppSettings { Network = new NetworkSettings { IncludeNames = ["A", "B"] }, Thresholds = new ThresholdSettings { DiskFreeGB = 1, DiskRecoveryFreeGB = 2 } };
        SettingsViewModel vm = Create(current);
        var errors = new List<string>();

        AppSettings built = vm.Build(errors);

        Assert.Empty(errors);
        Assert.Equal(SettingsSerializer.ToJson(current), SettingsSerializer.ToJson(built));
    }

    [Fact]
    public void InvalidSettingsFileShowsReplacementNotice()
    {
        Assert.Contains("有錯誤，下方為內建預設值", Create(valid: false).Notice, StringComparison.Ordinal);
        Assert.Contains("settings.json.bak", Create().Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshDoesNotOverwriteInput()
    {
        SettingsViewModel vm = Create();
        vm.Field("monitor.logSeconds").Text = "99";

        vm.Refresh(new RefreshData(null, new Core.Models.Alerts.AlertView(true, [], [])));

        Assert.Equal("99", vm.Field("monitor.logSeconds").Text);
    }
}
