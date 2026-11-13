using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>
/// <see cref="IStorageStatus"/> 實作；由 CSV 寫入端更新。執行緒安全。
/// </summary>
public sealed class StorageStatus : IStorageStatus
{
    private long _writtenRows;
    private long _lostRows;
    private string? _lastError;

    /// <inheritdoc />
    public long WrittenRows => Interlocked.Read(ref _writtenRows);

    /// <inheritdoc />
    public long LostRows => Interlocked.Read(ref _lostRows);

    /// <inheritdoc />
    public string? LastError => Volatile.Read(ref _lastError);

    /// <summary>記錄成功寫入，並清除最近錯誤。</summary>
    public void AddWritten(int rows)
    {
        Interlocked.Add(ref _writtenRows, rows);
        Volatile.Write(ref _lastError, null);
    }

    /// <summary>記錄遺失的資料列與原因。</summary>
    public void AddLost(int rows, string error)
    {
        Interlocked.Add(ref _lostRows, rows);
        Volatile.Write(ref _lastError, error);
    }
}
