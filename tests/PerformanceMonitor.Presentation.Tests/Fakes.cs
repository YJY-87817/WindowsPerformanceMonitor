using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.Presentation.Tests;

internal sealed class FakeSnapshots : ISnapshotProvider
{
    public event EventHandler<MonitorSnapshot>? SnapshotPublished
    {
        add { }
        remove { }
    }

    public MonitorSnapshot? Latest { get; set; }
}

internal sealed class FakeAlerts : IAlertProvider
{
    public AlertView Current { get; set; } = new(true, [], []);
}

internal sealed class FakeStorage : IStorageStatus
{
    public long WrittenRows { get; set; }

    public long LostRows { get; set; }

    public string? LastError { get; set; }
}

/// <summary>執行到取消為止；可指定擲出例外。</summary>
internal sealed class FakeSession : IMonitoringSession
{
    public int Runs { get; private set; }

    public Exception? Throws { get; set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Runs++;
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
        }
    }
}

internal sealed class FakeSettingsService : ISettingsService
{
    public string SettingsPath => @"C:\Users\me\AppData\Local\WindowsPerformanceMonitor\settings.json";

    public List<AppSettings> Saved { get; } = [];

    public IReadOnlyList<string> DirectoryErrors { get; set; } = [];

    public Exception? SaveThrows { get; set; }

    public IReadOnlyList<string> CheckDirectories(AppSettings settings) => DirectoryErrors;

    public void Save(AppSettings settings)
    {
        if (SaveThrows is not null)
        {
            throw SaveThrows;
        }

        Saved.Add(settings);
    }
}

internal sealed class FakeRestart : IApplicationRestart
{
    public int Restarts { get; private set; }

    public Task RestartAsync()
    {
        Restarts++;
        return Task.CompletedTask;
    }
}

internal static class Samples
{
    public static readonly DateTimeOffset Time = new(2026, 11, 13, 6, 0, 0, TimeSpan.Zero);

    public static MonitorSnapshot Snapshot(params ModuleResult[] results) =>
        new(new SampleId(Guid.Parse("0123456789abcdef0123456789abcdef")), Time, TimeSpan.FromHours(8), "PC", results, TimeSpan.FromMilliseconds(45.6), LoopOverrun: false);

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }
}
