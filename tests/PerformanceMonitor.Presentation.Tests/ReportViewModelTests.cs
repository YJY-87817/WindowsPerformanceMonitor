using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Presentation.ViewModels;
using PerformanceMonitor.Reporting;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Presentation.Tests;

internal sealed class FakeReportRunner : IReportRunner
{
    public List<ReportPeriod> Requests { get; } = [];

    public Exception? Throws { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public string OutputDirectory => Path.Combine("data", "Reports");

    public async Task<ReportResult> GenerateAsync(ReportPeriod period)
    {
        Requests.Add(period);
        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (Throws is not null)
        {
            throw Throws;
        }

        var builder = new ReportBuilder(period, TimeZoneInfo.Utc, 10);
        builder.AddDateWithoutData(period.From);
        return new ReportResult(Path.Combine(OutputDirectory, ReportService.DefaultFileName(period)), builder.Build(DateTimeOffset.UnixEpoch));
    }
}

internal sealed class FakeLauncher : IShellLauncher
{
    public List<string> Opened { get; } = [];

    public bool Fail { get; set; }

    public void Open(string path)
    {
        if (Fail)
        {
            throw new InvalidOperationException("沒有關聯的程式");
        }

        Opened.Add(path);
    }
}

public class ReportViewModelTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private readonly FakeReportRunner _runner = new();
    private readonly FakeLauncher _launcher = new();

    private ReportViewModel Create() => new(_runner, _launcher, () => Today);

    [Fact]
    public void StartsWithTodayAndQuickRanges()
    {
        ReportViewModel vm = Create();

        Assert.Equal("報告", vm.Title);
        Assert.Equal(new DateTime(2026, 11, 16), vm.From);
        Assert.Equal(new DateTime(2026, 11, 16), vm.To);
        Assert.Equal("2026-11-16；趨勢彙總每分鐘", vm.PeriodText);
        Assert.True(vm.CanGenerate);
        Assert.Contains(Path.Combine("data", "Reports"), vm.Notice, StringComparison.Ordinal);

        vm.YesterdayCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 11, 15), vm.From);
        Assert.Equal(new DateTime(2026, 11, 15), vm.To);

        vm.Last7DaysCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 11, 10), vm.From);
        Assert.Equal("2026-11-10 ～ 2026-11-16（7 天）；趨勢彙總每 10 分鐘", vm.PeriodText);

        vm.Last30DaysCommand.Execute(null);
        Assert.Equal(new DateTime(2026, 10, 18), vm.From);
        Assert.EndsWith("趨勢彙總每小時", vm.PeriodText, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPeriodCannotGenerate()
    {
        ReportViewModel vm = Create();

        vm.From = new DateTime(2026, 11, 17);
        Assert.False(vm.CanGenerate);
        Assert.False(vm.GenerateCommand.CanExecute(null));
        Assert.Contains("晚於", vm.PeriodError, StringComparison.Ordinal);
        Assert.Null(vm.PeriodText);

        vm.From = new DateTime(2025, 1, 1);
        Assert.Contains("366", vm.PeriodError, StringComparison.Ordinal);

        vm.From = null;
        Assert.Equal("請選擇起日與訖日", vm.PeriodError);

        vm.TodayCommand.Execute(null);
        Assert.Null(vm.PeriodError);
        Assert.True(vm.CanGenerate);
    }

    [Fact]
    public async Task GeneratesInBackgroundThenOpensReport()
    {
        ReportViewModel vm = Create();
        _runner.Gate = new TaskCompletionSource();
        vm.Last7DaysCommand.Execute(null);

        Task running = vm.GenerateCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.CanGenerate);
        Assert.StartsWith("正在產生", vm.Message, StringComparison.Ordinal);
        _runner.Gate.SetResult();
        await running;

        string expected = Path.Combine("data", "Reports", "Report_20261110-20261116.html");
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsError);
        Assert.Equal(expected, vm.ReportPath);
        Assert.Equal([expected], _launcher.Opened);
        Assert.Equal(new DateOnly(2026, 11, 10), Assert.Single(_runner.Requests).From);
        Assert.Contains("沒有資料的日期 1 天", vm.Summary[^1], StringComparison.Ordinal);
        Assert.Contains("無法計算缺失比例", vm.Summary[1], StringComparison.Ordinal);

        vm.OpenFolderCommand.Execute(null);
        Assert.Equal(Path.Combine("data", "Reports"), _launcher.Opened[^1]);
        Assert.True(vm.OpenReportCommand.CanExecute(null));
    }

    [Fact]
    public async Task WriteFailureIsShownAndCanRetry()
    {
        ReportViewModel vm = Create();
        _runner.Throws = new IOException("磁碟已滿");

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.True(vm.IsError);
        Assert.Equal("報告產生失敗：磁碟已滿", vm.Message);
        Assert.Null(vm.ReportPath);
        Assert.False(vm.OpenReportCommand.CanExecute(null));
        Assert.Empty(_launcher.Opened);
        Assert.True(vm.CanGenerate);
    }

    [Fact]
    public async Task OpenFailureKeepsReportPath()
    {
        ReportViewModel vm = Create();
        _launcher.Fail = true;

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ReportPath);
        Assert.True(vm.IsError);
        Assert.Contains("無法開啟", vm.Message, StringComparison.Ordinal);
    }
}
