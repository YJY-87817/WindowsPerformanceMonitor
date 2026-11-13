namespace PerformanceMonitor.Core.Enums;

/// <summary>
/// 錯誤分類（沿用舊規格 v1.5 §10.1）。詳細原因寫在錯誤訊息，不另增分類。
/// </summary>
public enum ErrorCode
{
    /// <summary>硬體、介面卡或工具不存在。</summary>
    Unavailable = 1,

    /// <summary>設定無法解析或超出範圍。</summary>
    ConfigurationError,

    /// <summary>外部命令或查詢逾時。</summary>
    Timeout,

    /// <summary>資料來源回傳格式不符或數值不合理。</summary>
    DataError,

    /// <summary>權限不足。</summary>
    AccessDenied,

    /// <summary>檔案被鎖定或無法寫入。</summary>
    IOError,

    /// <summary>暫時性失敗，下一次取樣重試。</summary>
    TransientError,

    /// <summary>未預期的程式例外。</summary>
    InternalError,
}
