using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Presentation.Text;
using PerformanceMonitor.Reporting.Html;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>
/// 報告頁（Phase 4c 確認）：選起訖日期（含「今天」「昨天」「最近 7 天」「最近 30 天」），在背景產生離線 HTML 報告
/// （不阻塞畫面，監控照常），完成後以預設瀏覽器開啟，並提供「開啟報告」「開啟資料夾」。
/// </summary>
public sealed class ReportViewModel : PageViewModel
{
    private readonly IReportRunner _runner;
    private readonly IShellLauncher _launcher;
    private readonly Func<DateOnly> _today;
    private DateTime? _from;
    private DateTime? _to;
    private bool _isBusy;
    private string? _message;
    private bool _isError;
    private string? _reportPath;
    private IReadOnlyList<string> _summary = [];

    /// <summary>建立。</summary>
    /// <param name="runner">產生報告。</param>
    /// <param name="launcher">開啟檔案或資料夾。</param>
    /// <param name="today">今天的本機日期。</param>
    public ReportViewModel(IReportRunner runner, IShellLauncher launcher, Func<DateOnly> today)
        : base(UiText.ReportPage)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(today);
        _runner = runner;
        _launcher = launcher;
        _today = today;
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, () => CanGenerate);
        TodayCommand = new RelayCommand(() => SetRange(0, 0));
        YesterdayCommand = new RelayCommand(() => SetRange(-1, -1));
        Last7DaysCommand = new RelayCommand(() => SetRange(-6, 0));
        Last30DaysCommand = new RelayCommand(() => SetRange(-29, 0));
        OpenReportCommand = new RelayCommand(() => Open(_reportPath!), () => _reportPath is not null);
        OpenFolderCommand = new RelayCommand(() => Open(_runner.OutputDirectory));
        SetRange(0, 0);
    }

    /// <summary>報告說明。</summary>
    public string Notice =>
        $"選擇期間（本機日期，含頭含尾，最長 {ReportPeriod.MaxDays} 天）後按「產生報告」。報告寫到 {_runner.OutputDirectory}（同名檔覆蓋），完成後以預設瀏覽器開啟；產生期間監控照常。";

    /// <summary>起日（DatePicker）。</summary>
    public DateTime? From
    {
        get => _from;
        set
        {
            if (SetProperty(ref _from, value))
            {
                OnPeriodChanged();
            }
        }
    }

    /// <summary>訖日（DatePicker）。</summary>
    public DateTime? To
    {
        get => _to;
        set
        {
            if (SetProperty(ref _to, value))
            {
                OnPeriodChanged();
            }
        }
    }

    /// <summary>期間錯誤（沒有錯誤時為 null）。</summary>
    public string? PeriodError => Period(out string? error) is null ? error : null;

    /// <summary>期間說明，例如「2026-11-01 ～ 2026-11-07（7 天）；趨勢每 10 分鐘」。</summary>
    public string? PeriodText => Period(out _) is ReportPeriod p
        ? $"{p}；趨勢彙總{ReportTimeline.GranularityFor(p.Days) switch
        {
            { TotalDays: >= 1 } => "每日",
            { TotalHours: >= 1 } => "每小時",
            { TotalMinutes: > 1 } g => string.Create(CultureInfo.InvariantCulture, $"每 {g.TotalMinutes:0} 分鐘"),
            _ => "每分鐘",
        }}"
        : null;

    /// <summary>是否正在產生。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                GenerateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>可以產生：期間正確且不在產生中。</summary>
    public bool CanGenerate => !IsBusy && Period(out _) is not null;

    /// <summary>操作結果訊息。</summary>
    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>訊息是否為錯誤。</summary>
    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    /// <summary>最後產生的報告路徑。</summary>
    public string? ReportPath
    {
        get => _reportPath;
        private set
        {
            if (SetProperty(ref _reportPath, value))
            {
                OpenReportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>最後產生的報告摘要（每項一行）。</summary>
    public IReadOnlyList<string> Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>產生報告。</summary>
    public IAsyncRelayCommand GenerateCommand { get; }

    /// <summary>期間設為今天。</summary>
    public IRelayCommand TodayCommand { get; }

    /// <summary>期間設為昨天。</summary>
    public IRelayCommand YesterdayCommand { get; }

    /// <summary>最近 7 天（含今天）。</summary>
    public IRelayCommand Last7DaysCommand { get; }

    /// <summary>最近 30 天（含今天）。</summary>
    public IRelayCommand Last30DaysCommand { get; }

    /// <summary>開啟最後產生的報告。</summary>
    public IRelayCommand OpenReportCommand { get; }

    /// <summary>開啟報告資料夾。</summary>
    public IRelayCommand OpenFolderCommand { get; }

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        // 報告不隨取樣更新。
    }

    /// <summary>摘要文字（供畫面與測試）。</summary>
    public static IReadOnlyList<string> Summarize(ReportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ReportModel m = result.Model;
        DataQuality q = m.Quality;
        string expected = q.ExpectedSamples is int e
            ? string.Create(CultureInfo.InvariantCulture, $"，預期 {e:#,0}，缺失 {HtmlReportRenderer.Ratio(q.MissingRatio)}")
            : "（沒有 Application Log，無法計算缺失比例）";
        return
        [
            $"期間：{m.Period}",
            string.Create(CultureInfo.InvariantCulture, $"指標 {m.Series.Count} 項；SystemMetrics 紀錄 {q.ActualSamples:#,0} 筆{expected}"),
            string.Create(CultureInfo.InvariantCulture, $"監控時段 {q.Sessions.Count} 個；告警事件 {m.Alerts.TotalEvents:#,0} 筆；程序紀錄 {m.Processes.Records:#,0} 次"),
            q.FileIssues.Count == 0 && q.DatesWithoutData.Count == 0
                ? "檔案：沒有缺少或損毀"
                : string.Create(CultureInfo.InvariantCulture, $"檔案問題 {q.FileIssues.Count} 個；沒有資料的日期 {q.DatesWithoutData.Count} 天（詳見報告「資料品質」）"),
        ];
    }

    private ReportPeriod? Period(out string? error)
    {
        if (From is not DateTime from || To is not DateTime to)
        {
            error = "請選擇起日與訖日";
            return null;
        }

        return ReportPeriod.TryCreate(DateOnly.FromDateTime(from), DateOnly.FromDateTime(to), out error);
    }

    private void SetRange(int fromOffset, int toOffset)
    {
        DateOnly today = _today();
        _from = today.AddDays(fromOffset).ToDateTime(TimeOnly.MinValue);
        _to = today.AddDays(toOffset).ToDateTime(TimeOnly.MinValue);
        OnPropertyChanged(nameof(From));
        OnPropertyChanged(nameof(To));
        OnPeriodChanged();
    }

    private void OnPeriodChanged()
    {
        OnPropertyChanged(nameof(PeriodError));
        OnPropertyChanged(nameof(PeriodText));
        OnPropertyChanged(nameof(CanGenerate));
        GenerateCommand?.NotifyCanExecuteChanged();
    }

    private async Task GenerateAsync()
    {
        if (Period(out _) is not ReportPeriod period)
        {
            return;
        }

        IsBusy = true;
        IsError = false;
        Message = $"正在產生 {period} 的報告…";
        try
        {
            ReportResult result = await _runner.GenerateAsync(period).ConfigureAwait(true);
            ReportPath = result.Path;
            Summary = Summarize(result);
            Message = $"已產生：{result.Path}";
            Open(result.Path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            IsError = true;
            Message = $"報告產生失敗：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Open(string path)
    {
        try
        {
            _launcher.Open(path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            IsError = true;
            Message = $"{Message}（無法開啟 {path}：{ex.Message}）";
        }
    }
}
