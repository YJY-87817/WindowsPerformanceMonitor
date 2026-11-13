using PerformanceMonitor.Application.Alerts;
using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>
/// 一次監控工作階段：接上 Log 觀察者與告警、執行 <see cref="MonitoringEngine"/>、寫停止 Log
/// （正常停止含本次 CSV 寫入／遺失列數；未預期例外寫「監控異常結束」與 Error Log 的 InternalError）。
/// </summary>
public sealed class MonitoringSession(
    MonitoringEngine engine,
    SnapshotStore store,
    MonitoringLogObserver observer,
    AlertService alerts,
    IAppLog log,
    IStorageStatus storage)
{
    private const string Source = "Monitor";

    /// <summary>執行到取消為止。</summary>
    public Task RunAsync(CancellationToken cancellationToken) =>
        RunCoreAsync(
            "監控開始",
            async () =>
            {
                await engine.RunAsync(cancellationToken).ConfigureAwait(false);
                return null;
            },
            "監控正常停止");

    /// <summary>單次取樣（主控台 <c>--once</c>），回傳快照。</summary>
    public async Task<MonitorSnapshot> RunOnceAsync(CancellationToken cancellationToken) =>
        (await RunCoreAsync("單次取樣開始", async () => await engine.RunOnceAsync(cancellationToken).ConfigureAwait(false), "單次取樣完成").ConfigureAwait(false))!;

    private async Task<MonitorSnapshot?> RunCoreAsync(string startMessage, Func<Task<MonitorSnapshot?>> run, string stopMessage)
    {
        void OnSnapshot(object? sender, MonitorSnapshot snapshot)
        {
            observer.Observe(snapshot);
            alerts.Observe(snapshot);
        }

        store.SnapshotPublished += OnSnapshot;
        log.Info(Source, startMessage);
        try
        {
            MonitorSnapshot? snapshot = await run().ConfigureAwait(false);
            log.Info(Source, $"{stopMessage}；本次 CSV 寫入 {storage.WrittenRows} 列、遺失 {storage.LostRows} 列。");
            return snapshot;
        }
        catch (Exception ex)
        {
            string message = ErrorMessageFormatter.Sanitize($"{ex.GetType().Name}: {ex.Message}") ?? ex.GetType().Name;
            log.Error(Source, $"監控異常結束：{message}");
            log.Write(LogChannel.Error, LogSeverity.Error, Source, $"[InternalError] {message}（處理：停止監控）");
            throw;
        }
        finally
        {
            store.SnapshotPublished -= OnSnapshot;
        }
    }
}
