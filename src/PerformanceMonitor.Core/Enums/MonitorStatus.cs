namespace PerformanceMonitor.Core.Enums;

/// <summary>
/// 監控模組及其資料來源的狀態（沿用舊規格 v1.5 §3.1.1）。
/// </summary>
public enum MonitorStatus
{
    /// <summary>成功取得有效資料；數值可以是 0。</summary>
    OK = 0,

    /// <summary>速率型數值只有一次取樣，尚未能計算。</summary>
    WarmingUp,

    /// <summary>硬體、介面卡或必要工具不存在，或目前環境不支援。</summary>
    Unavailable,

    /// <summary>資料來源存在，但查詢、解析或執行發生錯誤。</summary>
    Error,

    /// <summary>使用者透過設定停用。</summary>
    Disabled,
}
