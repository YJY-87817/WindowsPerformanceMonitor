using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Application.Tests;

/// <summary>可手動控制的時間來源（手寫假物件）。</summary>
internal sealed class FakeClock : IClock
{
    private readonly Queue<TimeSpan> _elapsed;

    public FakeClock(DateTimeOffset utcNow, params TimeSpan[] elapsed)
    {
        UtcNow = utcNow;
        _elapsed = new Queue<TimeSpan>(elapsed);
    }

    public DateTimeOffset UtcNow { get; }

    public TimeSpan Elapsed => _elapsed.Dequeue();
}
