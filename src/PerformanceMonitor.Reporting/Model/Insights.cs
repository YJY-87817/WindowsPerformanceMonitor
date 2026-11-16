namespace PerformanceMonitor.Reporting.Model;

/// <summary>一個尖峰時段（由相鄰的趨勢時段合併）。</summary>
/// <param name="Start">開始（第一個時段的起點）。</param>
/// <param name="End">結束（最後一個時段的終點）。</param>
/// <param name="Average">時段內平均（依各段筆數加權）。</param>
/// <param name="Max">時段內最大。</param>
/// <param name="Device">平均最高的那一段是哪個裝置（磁碟、GPU）；單一來源時為 null。</param>
public sealed record PeakPeriod(DateTimeOffset Start, DateTimeOffset End, double Average, double Max, string? Device);

/// <summary>一個指標的尖峰時段。</summary>
/// <param name="Title">標題，例如 <c>CPU 使用率</c>。</param>
/// <param name="Unit">單位。</param>
/// <param name="Periods">尖峰時段，依平均由高到低。</param>
/// <param name="ByDevice">是否由多個裝置合成（列出各時段最高的裝置）。</param>
public sealed record PeakList(string Title, string Unit, IReadOnlyList<PeakPeriod> Periods, bool ByDevice = false);

/// <summary>告警事件種類（Application Log 來源 <c>Alert</c>）。</summary>
public enum AlertLogKind
{
    /// <summary>告警。</summary>
    Warning,

    /// <summary>告警持續（冷卻結束後再次通知）。</summary>
    Repeat,

    /// <summary>已恢復。</summary>
    Recovered,
}

/// <summary>一筆告警事件。</summary>
/// <param name="Time">時間。</param>
/// <param name="Kind">種類。</param>
/// <param name="Target">對象，例如 <c>CPU</c>、<c>Disk C:</c>。</param>
/// <param name="ValueText">當時數值。</param>
/// <param name="Threshold">門檻說明。</param>
public sealed record AlertLogEvent(DateTimeOffset Time, AlertLogKind Kind, string Target, string ValueText, string Threshold);

/// <summary>一個告警對象的摘要。</summary>
/// <param name="Target">對象。</param>
/// <param name="Warnings">告警次數。</param>
/// <param name="Repeats">再次通知次數。</param>
/// <param name="Duration">告警總時間（告警到已恢復；沒有恢復紀錄時算到該次監控結束）。</param>
/// <param name="Unrecovered">是否有沒有恢復紀錄的告警。</param>
/// <param name="LastValueText">最後一次告警時的數值。</param>
public sealed record AlertSummary(string Target, int Warnings, int Repeats, TimeSpan Duration, bool Unrecovered, string? LastValueText);

/// <summary>告警事件（摘要與最近明細）。</summary>
/// <param name="Summaries">各對象摘要，依第一次出現順序。</param>
/// <param name="Recent">最近的事件（新到舊），最多 <see cref="MaxRecent"/> 筆。</param>
/// <param name="TotalEvents">期間內事件總數。</param>
public sealed record AlertReport(IReadOnlyList<AlertSummary> Summaries, IReadOnlyList<AlertLogEvent> Recent, int TotalEvents)
{
    /// <summary>明細最多列出的筆數（2026-11-16 確認）。</summary>
    public const int MaxRecent = 200;
}

/// <summary>一個程序名稱的統計。</summary>
/// <param name="Name">程序名稱（同名多個程序合併，不分 PID）。</param>
/// <param name="Appearances">進入 Top N 的紀錄次數。</param>
/// <param name="AppearanceRatio">進入 Top N 的比例（÷ 程序紀錄次數）。</param>
/// <param name="AverageMB">進入 Top N 時的平均 Working Set（同一次紀錄的同名程序相加）。</param>
/// <param name="MaxMB">最大 Working Set。</param>
public sealed record ProcessSummary(string Name, int Appearances, double AppearanceRatio, double AverageMB, double MaxMB);

/// <summary>高記憶體程序統計。</summary>
/// <param name="Records">程序紀錄次數（ProcessMetrics 中有資料的取樣數）。</param>
/// <param name="Top">依最大 Working Set 排序的前 <see cref="MaxCount"/> 名。</param>
public sealed record ProcessReport(int Records, IReadOnlyList<ProcessSummary> Top)
{
    /// <summary>列出的程序數（2026-11-16 確認）。</summary>
    public const int MaxCount = 10;
}
