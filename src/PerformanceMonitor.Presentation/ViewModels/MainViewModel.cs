using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.Text;
using PerformanceMonitor.Presentation.Trends;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>
/// 主視窗：左側導覽、監控狀態與開始／停止。畫面更新由組裝點的計時器依 <c>monitor.refreshSeconds</c>
/// 在 UI 執行緒呼叫 <see cref="Refresh"/>（與取樣週期分開）；監控本身在背景執行，不阻塞 UI。
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly MonitoringController _controller;
    private readonly ISnapshotProvider _snapshots;
    private readonly IAlertProvider _alerts;
    private readonly StartupStatus _startup;
    private PageViewModel _selectedPage;
    private string _stateText = string.Empty;
    private StatusKind _stateKind = StatusKind.Inactive;
    private string? _banner;

    /// <summary>建立。</summary>
    public MainViewModel(
        MonitoringController controller,
        ISnapshotProvider snapshots,
        IAlertProvider alerts,
        IStorageStatus storage,
        StartupStatus startup,
        AppSettings settings,
        TrendHistory trends,
        ISettingsService settingsService,
        IApplicationRestart restart)
    {
        ArgumentNullException.ThrowIfNull(storage);
        _controller = controller;
        _snapshots = snapshots;
        _alerts = alerts;
        _startup = startup;
        Overview = new OverviewViewModel();
        Status = new StatusViewModel(storage, startup);
        Trend = new TrendViewModel(trends, settings);
        Disk = new DiskViewModel();
        Network = new NetworkViewModel();
        Temperature = new TemperatureViewModel();
        Process = new ProcessViewModel();
        Settings = new SettingsViewModel(settings, startup.SettingsFileValid, settingsService, restart);
        Pages = [Overview, Trend, Disk, Network, Temperature, Process, Status, Settings];
        _selectedPage = Overview;
        StartCommand = new RelayCommand(Start, () => CanStart);
        StopCommand = new AsyncRelayCommand(StopAsync, () => CanStop);
        Refresh();
    }

    /// <summary>視窗標題。</summary>
    public string Title { get; } = UiText.AppTitle;

    /// <summary>總覽頁。</summary>
    public OverviewViewModel Overview { get; }

    /// <summary>狀態與錯誤頁。</summary>
    public StatusViewModel Status { get; }

    /// <summary>設定頁。</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>即時趨勢頁。</summary>
    public TrendViewModel Trend { get; }

    /// <summary>磁碟頁。</summary>
    public DiskViewModel Disk { get; }

    /// <summary>網路頁。</summary>
    public NetworkViewModel Network { get; }

    /// <summary>溫度頁。</summary>
    public TemperatureViewModel Temperature { get; }

    /// <summary>程序頁。</summary>
    public ProcessViewModel Process { get; }

    /// <summary>導覽清單。</summary>
    public IReadOnlyList<PageViewModel> Pages { get; }

    /// <summary>目前頁面。</summary>
    public PageViewModel SelectedPage
    {
        get => _selectedPage;
        set => SetProperty(ref _selectedPage, value ?? Overview);
    }

    /// <summary>監控狀態文字。</summary>
    public string StateText
    {
        get => _stateText;
        private set => SetProperty(ref _stateText, value);
    }

    /// <summary>監控狀態分類（顏色）。</summary>
    public StatusKind StateKind
    {
        get => _stateKind;
        private set => SetProperty(ref _stateKind, value);
    }

    /// <summary>醒目提示（設定錯誤、監控異常結束、CSV 遺失列）；沒有時為 null。</summary>
    public string? Banner
    {
        get => _banner;
        private set
        {
            if (SetProperty(ref _banner, value))
            {
                OnPropertyChanged(nameof(HasBanner));
            }
        }
    }

    /// <summary>是否有醒目提示。</summary>
    public bool HasBanner => Banner is not null;

    /// <summary>可以開始：設定與目錄沒有錯誤，且目前未在監控或停止中。</summary>
    public bool CanStart => _startup.CanMonitor && _controller.State is MonitoringState.Stopped or MonitoringState.Faulted;

    /// <summary>可以停止：監控中。</summary>
    public bool CanStop => _controller.State == MonitoringState.Running;

    /// <summary>開始監控。</summary>
    public IRelayCommand StartCommand { get; }

    /// <summary>停止監控（等待補寫最後一筆）。</summary>
    public IAsyncRelayCommand StopCommand { get; }

    /// <summary>依最新快照、告警與監控狀態更新畫面（在 UI 執行緒呼叫）。</summary>
    public void Refresh()
    {
        MonitoringState state = _controller.State;
        StateText = UiText.Monitoring(state);
        StateKind = UiText.Kind(state);

        var data = new RefreshData(_snapshots.Latest, _alerts.Current);
        foreach (PageViewModel page in Pages)
        {
            page.Refresh(data);
        }

        Banner = BuildBanner(state);
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private string? BuildBanner(MonitoringState state)
    {
        if (!_startup.CanMonitor)
        {
            return $"設定或資料目錄有錯誤，未開始監控：{string.Join("；", _startup.Errors)}（詳見「{UiText.StatusPage}」）";
        }

        if (state == MonitoringState.Faulted)
        {
            return $"監控異常結束：{_controller.FaultMessage}（可按「開始」重新監控）";
        }

        return Status.StorageKind == StatusKind.Error ? $"CSV 寫入有問題：{Status.StorageText}" : null;
    }

    private void Start()
    {
        _controller.Start();
        Refresh();
    }

    private async Task StopAsync()
    {
        Task stopping = _controller.StopAsync();
        Refresh(); // 立即顯示「停止中」
        await stopping.ConfigureAwait(true);
        Refresh();
    }
}
