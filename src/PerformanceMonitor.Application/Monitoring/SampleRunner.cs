using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>
/// 依序執行所有監控模組一次並組成快照。模組之間互相隔離：
/// 停用的模組回傳 <c>Disabled</c>；擲出未預期例外的模組回傳 <c>Error</c>／<c>InternalError</c>。
/// </summary>
public sealed class SampleRunner(IEnumerable<IMonitor> monitors, IClock clock, IHostInfo hostInfo)
{
    private readonly IMonitor[] _monitors = [.. monitors];

    /// <summary>已註冊的模組。</summary>
    public IReadOnlyList<IMonitor> Monitors => _monitors;

    /// <summary>取樣一次。取消時擲出 <see cref="OperationCanceledException"/>，該次不完整快照捨棄。</summary>
    /// <param name="interval">取樣週期；耗時超過時標示 LoopOverrun。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    public async Task<MonitorSnapshot> RunAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        TimeSpan start = clock.Elapsed;
        DateTimeOffset timestamp = clock.UtcNow;
        var results = new List<ModuleResult>(_monitors.Length);

        foreach (IMonitor monitor in _monitors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await SampleOneAsync(monitor, cancellationToken).ConfigureAwait(false));
        }

        TimeSpan duration = clock.Elapsed - start;
        return new MonitorSnapshot(
            SampleId.New(),
            timestamp,
            hostInfo.GetUtcOffset(timestamp),
            hostInfo.ComputerName,
            results,
            duration,
            LoopOverrun: duration > interval);
    }

    private async Task<ModuleResult> SampleOneAsync(IMonitor monitor, CancellationToken cancellationToken)
    {
        if (!monitor.IsEnabled)
        {
            return ModuleResult.Disabled(monitor.Name);
        }

        TimeSpan start = clock.Elapsed;
        ModuleResult result;
        try
        {
            result = await monitor.SampleAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // 模組隔離：任何未預期例外都轉為該模組的 Error，不得中止其他模組。
        catch (Exception ex)
#pragma warning restore CA1031
        {
            result = ModuleResult.Failed(monitor.Name, ErrorCode.InternalError, $"{ex.GetType().Name}: {ex.Message}");
        }

        return result with { Duration = clock.Elapsed - start };
    }
}
