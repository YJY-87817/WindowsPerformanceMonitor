namespace PerformanceMonitor.Core.Models.Alerts;

/// <summary>
/// 告警對象的持續狀態（舊規格 v1.5 §6.2）。WARNING 與 RECOVERED 是瞬間事件（見 <see cref="AlertEventType"/>），
/// 發送後分別進入 <see cref="Cooldown"/> 與 <see cref="Normal"/>。
/// </summary>
public enum AlertState
{
    /// <summary>未達觸發條件。</summary>
    Normal,

    /// <summary>已達門檻，尚未滿足持續時間。</summary>
    Pending,

    /// <summary>告警中，處於通知冷卻期間。</summary>
    Cooldown,

    /// <summary>告警中，已達恢復值但尚未滿足恢復持續時間。</summary>
    Recovering,
}

/// <summary>本次取樣對某個對象的判斷。</summary>
public enum AlertCondition
{
    /// <summary>達觸發條件。</summary>
    Breach,

    /// <summary>達恢復條件。</summary>
    Recovered,

    /// <summary>介於觸發值與恢復值之間（遲滯區間）。</summary>
    Hysteresis,

    /// <summary>資料不可用（Error／Unavailable／WarmingUp 等），不可視為高於或低於門檻。</summary>
    Unknown,
}

/// <summary>告警事件類型。</summary>
public enum AlertEventType
{
    /// <summary>告警（持續達門檻）。</summary>
    Warning,

    /// <summary>冷卻結束後仍異常，再次通知。</summary>
    Repeat,

    /// <summary>已恢復。</summary>
    Recovered,
}

/// <summary>一筆告警事件。</summary>
/// <param name="Time">發生時間（本機時間，取自該次快照）。</param>
/// <param name="Target">對象，例如 <c>CPU</c>、<c>RAM</c>、<c>Disk C:</c>。</param>
/// <param name="Type">事件類型。</param>
/// <param name="ValueText">當時數值。</param>
/// <param name="Threshold">門檻說明。</param>
public sealed record AlertEvent(DateTimeOffset Time, string Target, AlertEventType Type, string ValueText, string Threshold);

/// <summary>進行中的告警（狀態為 Cooldown 或 Recovering）。</summary>
/// <param name="Target">對象。</param>
/// <param name="State">狀態。</param>
/// <param name="ValueText">目前數值。</param>
/// <param name="Threshold">門檻說明。</param>
/// <param name="Since">告警開始時間（進入 Pending 的取樣時間，本機時間）。</param>
public sealed record ActiveAlert(string Target, AlertState State, string ValueText, string Threshold, DateTimeOffset Since);

/// <summary>畫面顯示用的告警狀態。</summary>
/// <param name="Enabled">告警是否啟用。</param>
/// <param name="Active">進行中的告警。</param>
/// <param name="Recent">最近事件（新到舊，最多 5 筆，含恢復）。</param>
public sealed record AlertView(bool Enabled, IReadOnlyList<ActiveAlert> Active, IReadOnlyList<AlertEvent> Recent);
