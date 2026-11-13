using System.ComponentModel;
using System.Globalization;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Gpu;

namespace PerformanceMonitor.Infrastructure.Tests;

/// <summary>移植舊版 Pester GpuMonitor.Tests.ps1 的案例，並加上溫度。</summary>
public class GpuMonitorTests
{
    private const string SmiPath = @"C:\Windows\System32\nvidia-smi.exe";

    private sealed class FakeLocator(string? path) : INvidiaSmiLocator
    {
        public string? Path { get; set; } = path;

        public int Finds { get; private set; }

        public IReadOnlyList<string> Candidates => [SmiPath, @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe"];

        public string? Find()
        {
            Finds++;
            return Path;
        }
    }

    private sealed class FakeRunner : ICommandRunner
    {
        public Queue<Func<CommandResult>> Results { get; } = new();

        public List<(string File, IReadOnlyList<string> Args, TimeSpan Timeout)> Calls { get; } = [];

        public Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls.Add((fileName, arguments, timeout));
            return Task.FromResult(Results.Dequeue()());
        }
    }

    private static CommandResult Ok(string output) => new(0, output, string.Empty, TimedOut: false);

    private static GpuMonitor Monitor(FakeLocator locator, FakeRunner runner, AppSettings? settings = null) =>
        new(settings ?? new AppSettings(), locator, runner);

    private static async Task<GpuResult> SampleAsync(GpuMonitor monitor) =>
        Assert.IsType<GpuResult>(await monitor.SampleAsync(CancellationToken.None));

    [Fact]
    public void ParsesMultipleGpusSortedByIndex()
    {
        GpuResult result = GpuMonitor.Parse("1, NVIDIA GeForce RTX 4060, 35, 2048, 8192, 61\r\n0, NVIDIA RTX A2000, 5, 512, 6144, 45\r\n");

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal([0, 1], result.Gpus.Select(g => g.GpuIndex));
        GpuItem g1 = result.Gpus[1];
        Assert.Equal("NVIDIA GeForce RTX 4060", g1.GpuName);
        Assert.Equal(35, g1.UsagePercent);
        Assert.Equal(2048, g1.MemoryUsedMB);
        Assert.Equal(8192, g1.MemoryTotalMB);
        Assert.Equal(61, g1.TemperatureC);
        Assert.Equal("GPU 1", g1.DeviceId);
    }

    [Fact]
    public void NameWithCommasIsJoined()
    {
        GpuItem g = Assert.Single(GpuMonitor.Parse("0, NVIDIA Quadro, Special, Edition, 10, 100, 1000, 50").Gpus);

        Assert.Equal("NVIDIA Quadro, Special, Edition", g.GpuName);
        Assert.Equal(10, g.UsagePercent);
        Assert.Equal(50, g.TemperatureC);
    }

    [Theory]
    [InlineData("[N/A]")]
    [InlineData("[Not Supported]")]
    [InlineData("N/A")]
    [InlineData("[Unknown Error]")]
    [InlineData("[Insufficient Permissions]")]
    public void UnsupportedFieldsAreEmptyAndGpuStaysOk(string marker)
    {
        GpuItem g = Assert.Single(GpuMonitor.Parse($"0, GPU, {marker}, 100, 1000, {marker}").Gpus);

        Assert.Equal(MonitorStatus.OK, g.Status);
        Assert.Null(g.UsagePercent);
        Assert.Null(g.TemperatureC);
        Assert.Equal(100, g.MemoryUsedMB);
    }

    [Fact]
    public void NonNumericValueIsErrorNotZero()
    {
        GpuItem g = Assert.Single(GpuMonitor.Parse("0, GPU, abc, 100, 1000, 50").Gpus);

        Assert.Equal(MonitorStatus.Error, g.Status);
        Assert.Equal(ErrorCode.DataError, g.ErrorCode);
        Assert.Null(g.UsagePercent);
        Assert.Equal("GPU", g.GpuName);
    }

    [Theory]
    [InlineData("0, GPU, 10, 100, 1000")] // 舊版 5 欄格式（缺溫度）
    [InlineData("garbage")]
    public void TooFewFieldsIsError(string line)
    {
        GpuItem g = Assert.Single(GpuMonitor.Parse(line).Gpus);

        Assert.Equal(MonitorStatus.Error, g.Status);
        Assert.Contains("欄位數量不符", g.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void NonIntegerIndexIsError()
    {
        GpuItem g = Assert.Single(GpuMonitor.Parse("x, GPU, 10, 100, 1000, 50").Gpus);

        Assert.Equal(MonitorStatus.Error, g.Status);
        Assert.Equal(-1, g.GpuIndex);
    }

    [Theory]
    [InlineData("0, GPU, 101, 100, 1000, 50", "使用率")]
    [InlineData("0, GPU, -1, 100, 1000, 50", "使用率")]
    [InlineData("0, GPU, 10, 1001, 1000, 50", "顯存")]
    [InlineData("0, GPU, 10, -1, 1000, 50", "顯存")]
    [InlineData("0, GPU, 10, 100, 1000, 151", "溫度")]
    [InlineData("0, GPU, 10, 100, 1000, -5", "溫度")]
    public void UnreasonableValuesAreError(string line, string expected)
    {
        GpuItem g = Assert.Single(GpuMonitor.Parse(line).Gpus);

        Assert.Equal(MonitorStatus.Error, g.Status);
        Assert.Equal(ErrorCode.DataError, g.ErrorCode);
        Assert.Contains(expected, g.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsingIgnoresCurrentCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            GpuItem g = Assert.Single(GpuMonitor.Parse("0, GPU, 12.5, 100, 1000, 50").Gpus);
            Assert.Equal(12.5, g.UsagePercent);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void OneBadGpuMakesModuleErrorButOthersKeepData()
    {
        GpuResult result = GpuMonitor.Parse("0, A, 10, 100, 1000, 50\n1, B, abc, 100, 1000, 50");

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(MonitorStatus.OK, result.Gpus[0].Status);
        Assert.Equal(10, result.Gpus[0].UsagePercent);
        Assert.Equal(MonitorStatus.Error, result.Gpus[1].Status);
    }

    [Fact]
    public void EmptyOutputIsDataError()
    {
        GpuResult result = GpuMonitor.Parse("  \r\n");

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.DataError, result.ErrorCode);
    }

    [Fact]
    public async Task SuccessReturnsOk()
    {
        var runner = new FakeRunner();
        runner.Results.Enqueue(() => Ok("0, RTX, 35, 2048, 8192, 61"));

        GpuResult result = await SampleAsync(Monitor(new FakeLocator(SmiPath), runner));

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(61, Assert.Single(result.Gpus).TemperatureC);
    }

    [Fact]
    public async Task CallsNvidiaSmiWithFixedArgumentsAndConfiguredTimeout()
    {
        var runner = new FakeRunner();
        runner.Results.Enqueue(() => Ok("0, RTX, 35, 2048, 8192, 61"));
        var settings = new AppSettings { Timeouts = new TimeoutSettings { GpuSeconds = 7 } };

        await SampleAsync(Monitor(new FakeLocator(SmiPath), runner, settings));

        (string file, IReadOnlyList<string> args, TimeSpan timeout) = Assert.Single(runner.Calls);
        Assert.Equal(SmiPath, file);
        Assert.Equal(["--query-gpu=index,name,utilization.gpu,memory.used,memory.total,temperature.gpu", "--format=csv,noheader,nounits"], args);
        Assert.Equal(TimeSpan.FromSeconds(7), timeout);
    }

    [Fact]
    public async Task NotFoundIsUnavailableAndRunsNothing()
    {
        var runner = new FakeRunner();

        GpuResult result = await SampleAsync(Monitor(new FakeLocator(null), runner));

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
        Assert.Contains("nvidia-smi", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task TimeoutIsError()
    {
        var runner = new FakeRunner();
        runner.Results.Enqueue(() => new CommandResult(-1, string.Empty, string.Empty, TimedOut: true));

        GpuResult result = await SampleAsync(Monitor(new FakeLocator(SmiPath), runner));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.Timeout, result.ErrorCode);
        Assert.Contains("3 秒", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void NonZeroExitIsErrorWithSummary()
    {
        GpuResult result = GpuMonitor.Interpret(new CommandResult(9, string.Empty, "NVIDIA-SMI has failed because it couldn't communicate with the NVIDIA driver.", false), 3);

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.TransientError, result.ErrorCode);
        Assert.Contains("結束碼 9", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("couldn't communicate", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void NonZeroExitWithoutOutputSaysSo()
    {
        GpuResult result = GpuMonitor.Interpret(new CommandResult(1, string.Empty, string.Empty, false), 3);

        Assert.Contains("（無輸出）", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void NoDevicesFoundIsUnavailable()
    {
        GpuResult result = GpuMonitor.Interpret(new CommandResult(6, "No devices were found", string.Empty, false), 3);

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
    }

    [Fact]
    public async Task StartFailureIsError()
    {
        var runner = new FakeRunner();
        runner.Results.Enqueue(() => throw new Win32Exception(5, "Access is denied"));

        GpuResult result = await SampleAsync(Monitor(new FakeLocator(SmiPath), runner));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.TransientError, result.ErrorCode);
        Assert.Contains("Access is denied", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecoversAfterFailureAndFindsToolEachSample()
    {
        var locator = new FakeLocator(null);
        var runner = new FakeRunner();
        runner.Results.Enqueue(() => Ok("0, RTX, 35, 2048, 8192, 61"));
        GpuMonitor monitor = Monitor(locator, runner);

        GpuResult first = await SampleAsync(monitor);
        locator.Path = SmiPath;
        GpuResult second = await SampleAsync(monitor);

        Assert.Equal(MonitorStatus.Unavailable, first.Status);
        Assert.Equal(MonitorStatus.OK, second.Status);
        Assert.Equal(2, locator.Finds);
    }

    [Fact]
    public void FollowsSettingsAndDescribesSource()
    {
        var disabled = Monitor(new FakeLocator(SmiPath), new FakeRunner(), new AppSettings { Gpu = new ModuleSettings { Enabled = false } });

        Assert.False(disabled.IsEnabled);
        Assert.Equal(ModuleNames.Gpu, disabled.Name);
        Assert.Equal($"GPU 來源：nvidia-smi（{SmiPath}）", disabled.SourceDescription);
        Assert.Contains("找不到", Monitor(new FakeLocator(null), new FakeRunner()).SourceDescription, StringComparison.Ordinal);
    }
}

public sealed class NvidiaSmiLocatorTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("wpm-smi-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReturnsFirstExistingCandidate()
    {
        string system = Path.Combine(_dir, "System32", "nvidia-smi.exe");
        string legacy = Path.Combine(_dir, "NVSMI", "nvidia-smi.exe");
        var locator = new NvidiaSmiLocator([system, legacy]);

        Assert.Null(locator.Find());

        Directory.CreateDirectory(Path.GetDirectoryName(legacy)!);
        File.WriteAllText(legacy, string.Empty);
        Assert.Equal(legacy, locator.Find());

        Directory.CreateDirectory(Path.GetDirectoryName(system)!);
        File.WriteAllText(system, string.Empty);
        Assert.Equal(system, locator.Find());
    }

    [Fact]
    public void DefaultCandidatesAreOnlyFixedLocations()
    {
        IReadOnlyList<string> candidates = new NvidiaSmiLocator().Candidates;

        if (!OperatingSystem.IsWindows())
        {
            Assert.Empty(candidates); // 非 Windows 沒有系統目錄與 Program Files
            return;
        }

        Assert.Equal(
            [
                Path.Combine(Environment.SystemDirectory, "nvidia-smi.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe"),
            ],
            candidates);
    }
}

public class ProcessCommandRunnerTests
{
    private static (string File, string[] Args) Shell(string windows, string unix) =>
        OperatingSystem.IsWindows() ? ("cmd.exe", ["/c", windows]) : ("/bin/sh", ["-c", unix]);

    [Fact]
    public async Task CapturesOutputAndExitCode()
    {
        (string file, string[] args) = Shell("echo hello& echo oops 1>&2& exit /b 3", "echo hello; echo oops 1>&2; exit 3");

        CommandResult result = await new ProcessCommandRunner().RunAsync(file, args, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.False(result.TimedOut);
        Assert.Equal(3, result.ExitCode);
        Assert.Equal("hello", result.StandardOutput.Trim());
        Assert.Equal("oops", result.StandardError.Trim());
    }

    [Fact]
    public async Task KillsProcessOnTimeout()
    {
        (string file, string[] args) = Shell("ping -n 30 127.0.0.1 >nul", "sleep 30");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        CommandResult result = await new ProcessCommandRunner().RunAsync(file, args, TimeSpan.FromMilliseconds(300), CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"耗時 {watch.Elapsed}");
    }

    [Fact]
    public async Task CancellationKillsProcessAndThrows()
    {
        (string file, string[] args) = Shell("ping -n 30 127.0.0.1 >nul", "sleep 30");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessCommandRunner().RunAsync(file, args, TimeSpan.FromSeconds(30), cancel.Token));
    }

    [Fact]
    public async Task MissingExecutableThrowsWin32Exception()
    {
        await Assert.ThrowsAsync<Win32Exception>(
            () => new ProcessCommandRunner().RunAsync(Path.Combine(Path.GetTempPath(), "no-such-nvidia-smi.exe"), [], TimeSpan.FromSeconds(1), CancellationToken.None));
    }
}
