using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.Text;
using PerformanceMonitor.Presentation.Trends;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>即時趨勢：最近 5 分鐘的 CPU／記憶體、網路合計、邏輯磁碟忙碌率、GPU 使用率與溫度（Phase 3b 確認）。</summary>
public sealed class TrendViewModel(TrendHistory history, AppSettings settings) : PageViewModel(UiText.TrendPage)
{
    private IReadOnlyList<TrendChart> _charts = [];

    /// <summary>各圖。</summary>
    public IReadOnlyList<TrendChart> Charts
    {
        get => _charts;
        private set => SetProperty(ref _charts, value);
    }

    /// <summary>時間範圍說明。</summary>
    public string RangeText { get; } = $"最近 {TrendHistory.Window.TotalMinutes:0} 分鐘；停止監控或無法取得的時段不畫線。";

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(settings);
        IReadOnlyList<TrendSample> samples = history.Samples;
        TimeSpan interval = TimeSpan.FromSeconds(settings.Monitor.SampleSeconds);
        const string NoData = "尚無資料";
        Charts =
        [
            TrendChartBuilder.Build("CPU 與記憶體使用率", "%", samples, TrendGroup.CpuMemory, interval, 100, 100, NoData),
            TrendChartBuilder.Build("網路下載／上傳合計", "Mbps", samples, TrendGroup.Network, interval, null, 1, "尚無資料（沒有納入統計的介面卡時無法使用）"),
            TrendChartBuilder.Build("邏輯磁碟忙碌率", "%", samples, TrendGroup.DiskBusy, interval, 100, 100, NoData),
            TrendChartBuilder.Build("GPU 使用率", "%", samples, TrendGroup.GpuUsage, interval, 100, 100, "無法使用（沒有 NVIDIA GPU 資料）"),
            TrendChartBuilder.Build("GPU 溫度", "°C", samples, TrendGroup.GpuTemperature, interval, null, 100, "無法使用（沒有 NVIDIA GPU 溫度資料）"),
        ];
    }
}
