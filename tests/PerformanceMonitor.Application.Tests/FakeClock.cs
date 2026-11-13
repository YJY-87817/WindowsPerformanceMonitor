using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Application.Tests;

/// <summary>
/// 手動控制的時間來源（手寫假物件）。<see cref="DelayAsync"/> 立即把時間往前推，
/// 讓排程測試不必真的等待。
/// </summary>
internal sealed class FakeClock : IClock
{
    private readonly Queue<TimeSpan> _scriptedElapsed;

    public FakeClock(params TimeSpan[] scriptedElapsed)
    {
        _scriptedElapsed = new Queue<TimeSpan>(scriptedElapsed);
    }

    public DateTimeOffset StartUtc { get; init; } = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);

    public TimeSpan Now { get; private set; }

    public List<TimeSpan> Delays { get; } = [];

    public DateTimeOffset UtcNow => StartUtc + Now;

    /// <summary>有預先排好的值時依序回傳（用來模擬時間倒退），否則回傳 <see cref="Now"/>。</summary>
    public TimeSpan Elapsed => _scriptedElapsed.Count > 0 ? _scriptedElapsed.Dequeue() : Now;

    public void Advance(TimeSpan amount) => Now += amount;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        Now += delay;
        return Task.CompletedTask;
    }
}
