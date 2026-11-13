using PerformanceMonitor.Application.Scheduling;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>
/// 監控主迴圈：暖機 → 依取樣格點取樣並發布快照 → 依 CSV 格點寫入紀錄。
/// 取樣與 CSV 使用同一起點的獨立格點；程序紀錄只在 CSV 紀錄時點檢查是否到期（舊規格 v1.5 §5.2）。
/// 停止時捨棄進行中的不完整取樣，並補寫最後一筆尚未寫入的完整快照（舊規格 v1.5 §4.4）。
/// </summary>
public sealed class MonitoringEngine(
    SampleRunner runner,
    IClock clock,
    AppSettings settings,
    SnapshotStore store,
    ISnapshotRecorder recorder)
{
    /// <summary>啟動暖機等待時間：速率型資料來源建立基準後等待，讓第一筆快照就有數值。</summary>
    public static readonly TimeSpan WarmUpDelay = TimeSpan.FromSeconds(1);

    /// <summary>執行到取消為止。取消屬於正常停止，不擲出例外。</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        MonitorSnapshot? unrecorded = null;
        try
        {
            await WarmUpAsync(cancellationToken).ConfigureAwait(false);

            TimeSpan start = clock.Elapsed;
            var sampleSchedule = new IntervalSchedule(TimeSpan.FromSeconds(settings.Monitor.SampleSeconds), start);
            var recordSchedule = new IntervalSchedule(TimeSpan.FromSeconds(settings.Monitor.LogSeconds), start);
            var processSchedule = new IntervalSchedule(TimeSpan.FromSeconds(settings.Process.LogIntervalSeconds), start);

            while (true)
            {
                await DelayUntilAsync(sampleSchedule.NextDue, cancellationToken).ConfigureAwait(false);

                MonitorSnapshot snapshot = await runner.RunAsync(sampleSchedule.Interval, cancellationToken).ConfigureAwait(false);
                TimeSpan now = clock.Elapsed;
                sampleSchedule.Advance(now);
                store.Publish(snapshot);
                unrecorded = settings.Logging.Enabled ? snapshot : null;

                if (unrecorded is not null && recordSchedule.IsDue(now))
                {
                    bool includeProcesses = settings.Process.LoggingEnabled && processSchedule.IsDue(now);
                    if (includeProcesses)
                    {
                        processSchedule.Advance(now);
                    }

                    recordSchedule.Advance(now);
                    await recorder.RecordAsync(snapshot, includeProcesses, cancellationToken).ConfigureAwait(false);
                    unrecorded = null;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 正常停止。
        }
        finally
        {
            if (unrecorded is not null)
            {
                await recorder.RecordAsync(unrecorded, settings.Process.LoggingEnabled, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 單次取樣（主控台 <c>--once</c>）：暖機 → 取樣一次 → 發布 → 寫入 CSV（含程序紀錄，若啟用），與連續模式行為一致。
    /// </summary>
    public async Task<MonitorSnapshot> RunOnceAsync(CancellationToken cancellationToken)
    {
        await WarmUpAsync(cancellationToken).ConfigureAwait(false);
        MonitorSnapshot snapshot = await runner.RunAsync(TimeSpan.FromSeconds(settings.Monitor.SampleSeconds), cancellationToken).ConfigureAwait(false);
        store.Publish(snapshot);
        if (settings.Logging.Enabled)
        {
            await recorder.RecordAsync(snapshot, settings.Process.LoggingEnabled, cancellationToken).ConfigureAwait(false);
        }

        return snapshot;
    }

    private async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        foreach (IMonitor monitor in runner.Monitors.Where(m => m.IsEnabled))
        {
            try
            {
                await monitor.WarmUpAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // 暖機失敗不影響啟動；第一次取樣會回報該模組狀態。
            catch (Exception)
#pragma warning restore CA1031
            {
            }
        }

        await clock.DelayAsync(WarmUpDelay, cancellationToken).ConfigureAwait(false);
    }

    private async Task DelayUntilAsync(TimeSpan due, CancellationToken cancellationToken)
    {
        TimeSpan wait = due - clock.Elapsed;
        if (wait > TimeSpan.Zero)
        {
            await clock.DelayAsync(wait, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
