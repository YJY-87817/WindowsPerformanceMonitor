namespace PerformanceMonitor.Core.Models;

/// <summary>
/// 一次完整取樣的所有模組結果。
/// </summary>
/// <param name="SampleId">取樣識別碼。</param>
/// <param name="TimestampUtc">取樣開始時間（UTC）。</param>
/// <param name="LocalOffset">取樣時本機時區的時差，用於 <see cref="TimestampLocal"/>。</param>
/// <param name="ComputerName">電腦名稱。</param>
/// <param name="Results">各模組結果，順序與模組註冊順序相同。</param>
/// <param name="SampleDuration">整次取樣耗時。</param>
/// <param name="LoopOverrun">整次取樣耗時是否超過取樣週期。</param>
public sealed record MonitorSnapshot(
    SampleId SampleId,
    DateTimeOffset TimestampUtc,
    TimeSpan LocalOffset,
    string ComputerName,
    IReadOnlyList<ModuleResult> Results,
    TimeSpan SampleDuration,
    bool LoopOverrun)
{
    /// <summary>含時區偏移的本機取樣時間。</summary>
    public DateTimeOffset TimestampLocal => TimestampUtc.ToOffset(LocalOffset);

    /// <summary>取得指定模組的結果；沒有時回傳 null。</summary>
    public ModuleResult? Find(string module) =>
        Results.FirstOrDefault(r => string.Equals(r.Module, module, StringComparison.Ordinal));

    /// <summary>取得第一個指定型別的結果；沒有時回傳 null。</summary>
    public T? Find<T>()
        where T : ModuleResult =>
        Results.OfType<T>().FirstOrDefault();
}
