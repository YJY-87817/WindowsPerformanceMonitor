using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Application.Services;

/// <summary>
/// 自我檢查結果：確認 DI 組裝完成、時間來源可用。
/// </summary>
/// <param name="CheckedAtUtc">檢查時間（UTC）。</param>
/// <param name="ClockIsMonotonic">單調計時是否正常前進。</param>
public sealed record SelfCheckResult(DateTimeOffset CheckedAtUtc, bool ClockIsMonotonic)
{
    /// <summary>所有檢查項目是否通過。</summary>
    public bool Passed => ClockIsMonotonic;
}

/// <summary>
/// 執行與硬體無關的基本檢查，供主控台 <c>--self-check</c> 與 CI Smoke Test 使用。
/// </summary>
public sealed class SelfCheckService(IClock clock)
{
    /// <summary>執行檢查。</summary>
    public SelfCheckResult Run()
    {
        TimeSpan first = clock.Elapsed;
        TimeSpan second = clock.Elapsed;
        return new SelfCheckResult(clock.UtcNow, second >= first && first >= TimeSpan.Zero);
    }
}
