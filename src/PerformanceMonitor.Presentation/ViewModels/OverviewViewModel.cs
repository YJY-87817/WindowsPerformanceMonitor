using System.Globalization;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Presentation.Text;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>總覽卡片。</summary>
/// <param name="Title">模組名稱。</param>
/// <param name="StatusText">狀態文字。</param>
/// <param name="Kind">狀態分類（顏色）。</param>
/// <param name="ValueText">目前數值摘要；無法取得時為「—」，不顯示假數值。</param>
/// <param name="Message">錯誤或無法使用的原因；沒有時為 null。</param>
public sealed record ModuleCard(string Title, string StatusText, StatusKind Kind, string ValueText, string? Message);

/// <summary>總覽：各模組摘要與進行中的告警。</summary>
public sealed class OverviewViewModel() : PageViewModel(UiText.OverviewPage)
{
    private IReadOnlyList<ModuleCard> _cards = [];
    private IReadOnlyList<string> _activeAlerts = [];
    private string _sampleText = "尚未取樣";

    /// <summary>各模組卡片（依模組順序；包含已停用的模組）。</summary>
    public IReadOnlyList<ModuleCard> Cards
    {
        get => _cards;
        private set => SetProperty(ref _cards, value);
    }

    /// <summary>進行中的告警（每筆一行）。</summary>
    public IReadOnlyList<string> ActiveAlerts
    {
        get => _activeAlerts;
        private set
        {
            if (SetProperty(ref _activeAlerts, value))
            {
                OnPropertyChanged(nameof(HasActiveAlerts));
            }
        }
    }

    /// <summary>是否有進行中的告警。</summary>
    public bool HasActiveAlerts => ActiveAlerts.Count > 0;

    /// <summary>最後取樣時間與耗時。</summary>
    public string SampleText
    {
        get => _sampleText;
        private set => SetProperty(ref _sampleText, value);
    }

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        MonitorSnapshot? snapshot = data.Snapshot;
        if (snapshot is not null)
        {
            Cards = [.. snapshot.Results.Select(r => new ModuleCard(
                UiText.ModuleTitle(r.Module), UiText.Status(r.Status), UiText.Kind(r.Status), ModuleSummary.For(r), r.ErrorMessage))];
            SampleText = string.Create(
                CultureInfo.InvariantCulture,
                $"最後取樣：{snapshot.TimestampLocal:yyyy-MM-dd HH:mm:ss}，耗時 {snapshot.SampleDuration.TotalMilliseconds:0} ms{(snapshot.LoopOverrun ? "（超過取樣週期）" : string.Empty)}");
        }

        ActiveAlerts = !data.Alerts.Enabled
            ? []
            : [.. data.Alerts.Active.Select(a => string.Create(
                CultureInfo.InvariantCulture,
                $"{a.Target}：{UiText.AlertState(a.State)}，目前 {a.ValueText}（{a.Threshold}），自 {a.Since:HH:mm:ss} 起"))];
    }
}
