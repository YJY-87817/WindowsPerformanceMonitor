using PerformanceMonitor.Application.Monitoring;

namespace PerformanceMonitor.Application.Tests;

public class MonitoringControllerTests
{
    /// <summary>執行到取消為止的假工作階段；可指定擲出例外。</summary>
    private sealed class FakeSession : IMonitoringSession
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Runs { get; private set; }

        public Exception? Throws { get; set; }

        public bool SawCancellation { get; private set; }

        public Task Started => _started.Task;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Runs++;
            _started.TrySetResult();
            if (Throws is not null)
            {
                throw Throws;
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                SawCancellation = true;
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    [Fact]
    public async Task StartRunsInBackgroundAndStopWaitsForEnd()
    {
        var session = new FakeSession();
        var controller = new MonitoringController(session);
        int changes = 0;
        controller.StateChanged += (_, _) => Interlocked.Increment(ref changes);

        controller.Start();
        Assert.Equal(MonitoringState.Running, controller.State);
        await session.Started;

        await controller.StopAsync();

        Assert.True(session.SawCancellation);
        Assert.Equal(MonitoringState.Stopped, controller.State);
        Assert.Null(controller.FaultMessage);
        Assert.True(changes >= 3); // Running、Stopping、Stopped
    }

    [Fact]
    public async Task StartTwiceRunsOnce()
    {
        var session = new FakeSession();
        var controller = new MonitoringController(session);

        controller.Start();
        controller.Start();
        await session.Started;
        await controller.StopAsync();

        Assert.Equal(1, session.Runs);
    }

    [Fact]
    public async Task CanRestartAfterStop()
    {
        var session = new FakeSession();
        var controller = new MonitoringController(session);

        controller.Start();
        await session.Started;
        await controller.StopAsync();
        controller.Start();
        await WaitUntilAsync(() => session.Runs == 2);
        await controller.StopAsync();

        Assert.Equal(MonitoringState.Stopped, controller.State);
    }

    [Fact]
    public async Task StopWithoutStartCompletes()
    {
        var controller = new MonitoringController(new FakeSession());

        await controller.StopAsync();

        Assert.Equal(MonitoringState.Stopped, controller.State);
    }

    [Fact]
    public async Task FailureIsFaultedWithMessageAndCanStartAgain()
    {
        var session = new FakeSession { Throws = new InvalidOperationException("壞掉了\n第二行") };
        var controller = new MonitoringController(session);

        controller.Start();
        await WaitUntilAsync(() => controller.State == MonitoringState.Faulted);

        Assert.Equal("InvalidOperationException: 壞掉了", controller.FaultMessage);

        session.Throws = null;
        controller.Start();
        await WaitUntilAsync(() => session.Runs == 2);
        Assert.Equal(MonitoringState.Running, controller.State);
        Assert.Null(controller.FaultMessage);
        await controller.StopAsync();
    }

    [Fact]
    public async Task ConcurrentStopsBothWait()
    {
        var session = new FakeSession();
        var controller = new MonitoringController(session);
        controller.Start();
        await session.Started;

        await Task.WhenAll(controller.StopAsync(), controller.StopAsync());

        Assert.Equal(MonitoringState.Stopped, controller.State);
    }
}
