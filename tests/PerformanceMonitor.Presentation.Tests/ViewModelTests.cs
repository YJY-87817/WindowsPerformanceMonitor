using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Presentation.Text;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.Presentation.Tests;

public class ViewModelTests
{
    private readonly FakeSnapshots _snapshots = new();
    private readonly FakeAlerts _alerts = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeSession _session = new();
    private readonly MonitoringController _controller;

    public ViewModelTests() => _controller = new MonitoringController(_session);

    private MainViewModel Create(StartupStatus? startup = null) =>
        new(_controller, _snapshots, _alerts, _storage, startup ?? new StartupStatus([], []));

    [Fact]
    public void BeforeFirstSampleShowsNoCardsAndCanStart()
    {
        MainViewModel vm = Create();

        Assert.Equal("Windows 效能監控", vm.Title);
        Assert.Empty(vm.Overview.Cards);
        Assert.Equal("尚未取樣", vm.Overview.SampleText);
        Assert.Equal("已停止", vm.StateText);
        Assert.True(vm.CanStart);
        Assert.False(vm.CanStop);
        Assert.Equal([UiText.OverviewPage, UiText.StatusPage], vm.Pages.Select(p => p.Title));
        Assert.Same(vm.Overview, vm.SelectedPage);
        Assert.False(vm.HasBanner);
    }

    [Fact]
    public void RefreshBuildsCardsInModuleOrderIncludingProblems()
    {
        _snapshots.Latest = Samples.Snapshot(
            new CpuResult(MonitorStatus.OK, 25.55),
            ModuleResult.Unavailable(ModuleNames.Gpu, "找不到 nvidia-smi"),
            ModuleResult.Disabled(ModuleNames.Network));
        MainViewModel vm = Create();

        vm.Refresh();

        Assert.Equal(
            [
                new ModuleCard("CPU", "正常（OK）", StatusKind.Ok, "25.6%", null),
                new ModuleCard("NVIDIA GPU", "無法使用（Unavailable）", StatusKind.Inactive, "—", "找不到 nvidia-smi"),
                new ModuleCard("網路", "已停用（Disabled）", StatusKind.Inactive, "—", null),
            ],
            vm.Overview.Cards);
        Assert.Equal("最後取樣：2026-11-13 14:00:00，耗時 46 ms", vm.Overview.SampleText);
    }

    [Fact]
    public void OverrunIsShown()
    {
        _snapshots.Latest = Samples.Snapshot(new CpuResult(MonitorStatus.OK, 1)) with { LoopOverrun = true };
        MainViewModel vm = Create();

        Assert.EndsWith("（超過取樣週期）", vm.Overview.SampleText, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveAlertsAreListed()
    {
        _alerts.Current = new AlertView(true, [new ActiveAlert("CPU", AlertState.Cooldown, "95.0%", "≥ 90%", Samples.Time.ToOffset(TimeSpan.FromHours(8)))], []);
        MainViewModel vm = Create();

        Assert.True(vm.Overview.HasActiveAlerts);
        Assert.Equal(["CPU：告警中，目前 95.0%（≥ 90%），自 14:00:00 起"], vm.Overview.ActiveAlerts);
    }

    [Fact]
    public void StatusPageListsModulesDeviceIssuesAlertsAndStorage()
    {
        _snapshots.Latest = Samples.Snapshot(
            new CpuResult(MonitorStatus.OK, 1),
            new DiskResult(MonitorStatus.Error, [
                new DiskItem(MonitorStatus.OK, "C:", null, 100, 1, 99, 1, 99),
                new DiskItem(MonitorStatus.Error, "D:", null, null, null, null, null, null) { ErrorCode = ErrorCode.DataError, ErrorMessage = "容量為 0" },
            ]));
        _alerts.Current = new AlertView(true, [], [new AlertEvent(Samples.Time.ToOffset(TimeSpan.FromHours(8)), "RAM", AlertEventType.Recovered, "70.0%", "< 85%")]);
        _storage.WrittenRows = 12;
        MainViewModel vm = Create();

        StatusViewModel status = vm.Status;
        Assert.Equal(["CPU", "磁碟容量"], status.Modules.Select(m => m.Module));
        StatusRow d = Assert.Single(status.DeviceIssues);
        Assert.Equal(new StatusRow("磁碟容量", "D:", "錯誤（Error）", StatusKind.Error, "DataError", "容量為 0"), d);
        Assert.Equal(["11-13 14:00:00  RAM 已恢復：70.0%（< 85%）"], status.RecentAlerts);
        Assert.Equal("CSV 已寫入 12 列，遺失 0 列", status.StorageText);
        Assert.Equal(StatusKind.Ok, status.StorageKind);
        Assert.False(vm.HasBanner);
    }

    [Theory]
    [InlineData(false, "告警已停用")]
    [InlineData(true, "沒有告警事件")]
    public void RecentAlertsPlaceholder(bool enabled, string expected)
    {
        _alerts.Current = new AlertView(enabled, [], []);

        Assert.Equal([expected], Create().Status.RecentAlerts);
    }

    [Fact]
    public void LostCsvRowsAreHighlighted()
    {
        _storage.WrittenRows = 5;
        _storage.LostRows = 2;
        _storage.LastError = "檔案被鎖定";
        MainViewModel vm = Create();

        Assert.Equal(StatusKind.Error, vm.Status.StorageKind);
        Assert.Equal("CSV 寫入有問題：CSV 已寫入 5 列，遺失 2 列；最近錯誤：檔案被鎖定", vm.Banner);
    }

    [Fact]
    public void StartupErrorsBlockStartAndShowBanner()
    {
        MainViewModel vm = Create(new StartupStatus(["設定檔（settings.json）：monitor.sampleSeconds 必須介於 1～60"], ["找不到設定檔"]));

        Assert.False(vm.CanStart);
        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.StartsWith("設定或資料目錄有錯誤，未開始監控：設定檔（settings.json）", vm.Banner, StringComparison.Ordinal);
        Assert.Equal(["錯誤：設定檔（settings.json）：monitor.sampleSeconds 必須介於 1～60", "警告：找不到設定檔"], vm.Status.StartupLines);
    }

    [Fact]
    public async Task StartAndStopCommandsControlMonitoring()
    {
        MainViewModel vm = Create();

        vm.StartCommand.Execute(null);
        Assert.Equal("監控中", vm.StateText);
        Assert.Equal(StatusKind.Ok, vm.StateKind);
        Assert.False(vm.CanStart);
        Assert.True(vm.StopCommand.CanExecute(null));
        await Samples.WaitUntilAsync(() => _session.Runs == 1);

        await vm.StopCommand.ExecuteAsync(null);

        Assert.Equal(MonitoringState.Stopped, _controller.State);
        Assert.Equal("已停止", vm.StateText);
        Assert.True(vm.CanStart);
        Assert.False(vm.CanStop);
    }

    [Fact]
    public async Task FaultIsShownAndCanRestart()
    {
        _session.Throws = new InvalidOperationException("壞掉了");
        MainViewModel vm = Create();
        vm.StartCommand.Execute(null);
        await Samples.WaitUntilAsync(() => _controller.State == MonitoringState.Faulted);

        vm.Refresh();

        Assert.Equal("監控異常結束", vm.StateText);
        Assert.Equal(StatusKind.Error, vm.StateKind);
        Assert.Equal("監控異常結束：InvalidOperationException: 壞掉了（可按「開始」重新監控）", vm.Banner);
        Assert.True(vm.CanStart);
    }

    [Fact]
    public void SelectingNullFallsBackToOverview()
    {
        MainViewModel vm = Create();
        vm.SelectedPage = vm.Status;
        Assert.Same(vm.Status, vm.SelectedPage);

        vm.SelectedPage = null!;

        Assert.Same(vm.Overview, vm.SelectedPage);
    }

    [Fact]
    public void RefreshRaisesPropertyChanged()
    {
        MainViewModel vm = Create();
        var changed = new List<string?>();
        vm.Overview.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        _snapshots.Latest = Samples.Snapshot(new CpuResult(MonitorStatus.OK, 1));

        vm.Refresh();

        Assert.Contains(nameof(OverviewViewModel.Cards), changed);
        Assert.Contains(nameof(OverviewViewModel.SampleText), changed);
    }
}
