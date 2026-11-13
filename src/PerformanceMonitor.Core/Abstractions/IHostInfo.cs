namespace PerformanceMonitor.Core.Abstractions;

/// <summary>
/// 執行電腦的資訊：電腦名稱與本機時區。
/// </summary>
public interface IHostInfo
{
    /// <summary>電腦名稱。</summary>
    string ComputerName { get; }

    /// <summary>指定 UTC 時間在本機時區的時差（含日光節約時間）。</summary>
    TimeSpan GetUtcOffset(DateTimeOffset utcTime);
}
