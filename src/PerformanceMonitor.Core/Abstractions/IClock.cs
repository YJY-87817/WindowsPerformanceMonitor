namespace PerformanceMonitor.Core.Abstractions;

/// <summary>
/// 時間來源。時間戳使用系統時間；經過時間使用單調計時，不受系統時間調整影響。
/// </summary>
public interface IClock
{
    /// <summary>目前的 UTC 系統時間，用於資料時間戳。</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>單調遞增的經過時間，用於排程與速率計算。</summary>
    TimeSpan Elapsed { get; }

    /// <summary>等待指定時間；可取消。測試可用假時間來源立即推進時間。</summary>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
