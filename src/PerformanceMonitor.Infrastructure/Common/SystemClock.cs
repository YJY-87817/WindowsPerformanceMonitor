using System.Diagnostics;
using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Infrastructure.Common;

/// <summary>
/// 以系統時間與 <see cref="Stopwatch"/> 實作的時間來源。
/// </summary>
public sealed class SystemClock : IClock
{
    private readonly long _startTimestamp = Stopwatch.GetTimestamp();

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);
}
