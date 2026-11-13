namespace PerformanceMonitor.Core.Abstractions;

/// <summary>
/// CSV 與 Log 寫入統計，供畫面醒目顯示與停止時寫入 Log。
/// </summary>
public interface IStorageStatus
{
    /// <summary>已寫入的 CSV 資料列數。</summary>
    long WrittenRows { get; }

    /// <summary>因鎖定或錯誤而遺失的 CSV 資料列數。</summary>
    long LostRows { get; }

    /// <summary>最近一次 CSV 寫入錯誤；之後成功寫入會清除。</summary>
    string? LastError { get; }
}
