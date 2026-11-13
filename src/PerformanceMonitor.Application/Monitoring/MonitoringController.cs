using PerformanceMonitor.Core.Common;

namespace PerformanceMonitor.Application.Monitoring;

/// <summary>監控狀態（畫面顯示用）。</summary>
public enum MonitoringState
{
    /// <summary>未監控。</summary>
    Stopped,

    /// <summary>監控中。</summary>
    Running,

    /// <summary>停止中：等待進行中的取樣結束並補寫最後一筆。</summary>
    Stopping,

    /// <summary>監控異常結束（見 <see cref="MonitoringController.FaultMessage"/>）。</summary>
    Faulted,
}

/// <summary>
/// 在背景執行連續監控並提供開始／停止（WPF 使用）：監控不在 UI 執行緒執行；
/// 停止時取消並等待監控結束（補寫最後一筆完整快照、寫停止 Log），之後不再產生新的取樣。執行緒安全。
/// </summary>
public sealed class MonitoringController(IMonitoringSession session)
{
    private readonly Lock _gate = new();
    private CancellationTokenSource? _cancellation;
    private Task? _running;
    private MonitoringState _state = MonitoringState.Stopped;
    private string? _faultMessage;

    /// <summary>狀態改變時觸發（可能在背景執行緒）。</summary>
    public event EventHandler? StateChanged;

    /// <summary>目前狀態。</summary>
    public MonitoringState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>異常結束的原因（第一行、最長 200 字）；其他狀態為 null。</summary>
    public string? FaultMessage
    {
        get
        {
            lock (_gate)
            {
                return _faultMessage;
            }
        }
    }

    /// <summary>開始監控；已在監控中或停止中時不做任何事。</summary>
    public void Start()
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_state is MonitoringState.Running or MonitoringState.Stopping)
            {
                return;
            }

            cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            _faultMessage = null;
            _state = MonitoringState.Running;
            _running = Task.Run(() => RunAsync(cancellation));
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>停止監控並等待結束；未在監控中時立即完成。</summary>
    public async Task StopAsync()
    {
        Task? running;
        lock (_gate)
        {
            if (_state != MonitoringState.Running)
            {
                running = _state == MonitoringState.Stopping ? _running : null;
            }
            else
            {
                _state = MonitoringState.Stopping;
                _cancellation!.Cancel();
                running = _running;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        if (running is not null)
        {
            await running.ConfigureAwait(false);
        }
    }

    private async Task RunAsync(CancellationTokenSource cancellation)
    {
        string? fault = null;
        try
        {
            await session.RunAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 正常停止。
        }
#pragma warning disable CA1031 // 監控異常結束時保留原因給畫面顯示（MonitoringSession 已寫 Log），不讓背景工作的例外遺失。
        catch (Exception ex)
#pragma warning restore CA1031
        {
            fault = ErrorMessageFormatter.Sanitize($"{ex.GetType().Name}: {ex.Message}");
        }

        lock (_gate)
        {
            _state = fault is null ? MonitoringState.Stopped : MonitoringState.Faulted;
            _faultMessage = fault;
            _cancellation = null;
        }

        cancellation.Dispose();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
