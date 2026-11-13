namespace PerformanceMonitor.Core.Models;

/// <summary>
/// 同一次完整取樣的識別碼；以 32 位十六進位字串寫入 CSV，關聯各分項資料。
/// </summary>
public readonly record struct SampleId(Guid Value)
{
    /// <summary>產生新的識別碼。</summary>
    public static SampleId New() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("N");
}
