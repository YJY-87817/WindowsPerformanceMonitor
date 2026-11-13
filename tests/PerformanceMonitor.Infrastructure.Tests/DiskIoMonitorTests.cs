using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Disk;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class DiskIoMonitorTests : IDisposable
{
    private const double MB = 1_048_576;

    private readonly FakePdhFactory _pdh = new();
    private readonly FakeDrives _drives = new();
    private readonly DiskIoMonitor _monitor;

    public DiskIoMonitorTests() => _monitor = new DiskIoMonitor(new AppSettings(), _pdh, _drives);

    public void Dispose() => _monitor.Dispose();

    private sealed class FakeDrives : IDriveSource
    {
        public string[] Drives { get; set; } = ["C:"];

        public Exception? Throws { get; set; }

        public IReadOnlyList<DriveReading> GetFixedDrives() =>
            Throws is not null ? throw Throws : [.. Drives.Select(d => new DriveReading(d, null, 1, 1))];
    }

    /// <summary>每個執行個體 7 個計數器讀值（依 DiskIoCounterSet.CounterNames 順序）。</summary>
    private static DiskIoCounterSet Set(params (string Name, double[] Values)[] instances) =>
        DiskIoCounterSet.From([.. Enumerable.Range(0, 7).Select(i => PdhArrayValue.Ok(
            instances.ToDictionary(x => x.Name, x => new PdhValue(PdhReadStatus.Ok, x.Values[i], 0), StringComparer.OrdinalIgnoreCase)))]);

    private static DiskIoCounterSet Uniform(PdhArrayValue value) => DiskIoCounterSet.From([.. Enumerable.Repeat(value, 7)]);

    private static readonly DiskIoCounterSet NoPhysical = Set();

    // 讀 10 MB/s、寫 5 MB/s、120 讀／80 寫 IOPS、讀延遲 2 ms、寫延遲 4.5 ms、閒置 75%。
    private static readonly double[] Busy = [10 * MB, 5 * MB, 120, 80, 0.002, 0.0045, 75];

    [Fact]
    public void ConvertsCountersToUnits()
    {
        DiskIoResult result = DiskIoMonitor.Calculate(["C:"], Set(("C:", Busy)), NoPhysical);

        Assert.Equal(MonitorStatus.OK, result.Status);
        DiskIoItem c = Assert.Single(result.Disks);
        Assert.Equal(DiskIoScope.Logical, c.Scope);
        Assert.Equal("C:", c.Disk);
        Assert.Equal("C:", c.DeviceId);
        Assert.Equal(10, c.ReadMBps);
        Assert.Equal(5, c.WriteMBps);
        Assert.Equal(120, c.ReadIops);
        Assert.Equal(80, c.WriteIops);
        Assert.Equal(2, c.ReadLatencyMs!.Value, 9);
        Assert.Equal(4.5, c.WriteLatencyMs!.Value, 9);
        Assert.Equal(25, c.BusyPercent);
    }

    [Theory]
    [InlineData(130, 0)] // 閒置率可能略超過 100
    [InlineData(0, 100)]
    public void ClampsBusyPercent(double idle, double expected)
    {
        double[] values = [0, 0, 0, 0, 0, 0, idle];
        DiskIoResult result = DiskIoMonitor.Calculate(["C:"], Set(("C:", values)), NoPhysical);

        Assert.Equal(expected, Assert.Single(result.Disks).BusyPercent);
    }

    [Fact]
    public void LogicalDisksFollowFixedDrivesAndPhysicalDisksExcludeTotal()
    {
        DiskIoCounterSet logical = Set(("D:", Busy), ("C:", Busy), ("HarddiskVolume1", Busy), ("_Total", Busy), ("E:", Busy));
        DiskIoCounterSet physical = Set(("10 F:", Busy), ("1", Busy), ("_Total", Busy), ("0 C: D:", Busy));

        DiskIoResult result = DiskIoMonitor.Calculate(["D:", "C:"], logical, physical);

        Assert.Equal(
            ["C:", "D:", "PhysicalDisk(0 C: D:)", "PhysicalDisk(1)", "PhysicalDisk(10 F:)"],
            result.Disks.Select(d => d.DeviceId));
        Assert.Equal(
            [DiskIoScope.Logical, DiskIoScope.Logical, DiskIoScope.Physical, DiskIoScope.Physical, DiskIoScope.Physical],
            result.Disks.Select(d => d.Scope));
        Assert.Equal("0 C: D:", result.Disks[2].Disk);
    }

    [Fact]
    public void FixedDriveWithoutCounterIsUnavailableOnly()
    {
        DiskIoResult result = DiskIoMonitor.Calculate(["C:", "F:"], Set(("C:", Busy)), NoPhysical);

        Assert.Equal(MonitorStatus.OK, result.Disks[0].Status);
        DiskIoItem f = result.Disks[1];
        Assert.Equal(MonitorStatus.Unavailable, f.Status);
        Assert.Equal(ErrorCode.Unavailable, f.ErrorCode);
        Assert.Null(f.ReadMBps);
        Assert.Equal(MonitorStatus.OK, result.Status); // 部分正常 → OK（StatusAggregator 規則）
    }

    [Fact]
    public void WithoutDriveListUsesLetterInstances()
    {
        DiskIoResult result = DiskIoMonitor.Calculate(null, Set(("C:", Busy), ("HarddiskVolume3", Busy), ("_Total", Busy)), NoPhysical);

        Assert.Equal(["C:"], result.Disks.Select(d => d.Disk));
    }

    [Fact]
    public void FirstReadIsWarmingUp()
    {
        DiskIoCounterSet notReady = Uniform(PdhArrayValue.Empty(PdhReadStatus.NotReady, 0xC0000BC6));

        DiskIoResult result = DiskIoMonitor.Calculate(["C:"], notReady, notReady);

        Assert.Equal(MonitorStatus.WarmingUp, result.Status);
        DiskIoItem c = Assert.Single(result.Disks);
        Assert.Equal(MonitorStatus.WarmingUp, c.Status);
        Assert.Null(c.ErrorCode);
    }

    [Fact]
    public void InstanceNotReadyIsWarmingUp()
    {
        var array = PdhArrayValue.Ok(new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["C:"] = new(PdhReadStatus.NotReady, 0, 0xC0000BBA),
        });

        DiskIoResult result = DiskIoMonitor.Calculate(["C:"], Uniform(array), NoPhysical);

        Assert.Equal(MonitorStatus.WarmingUp, Assert.Single(result.Disks).Status);
    }

    [Fact]
    public void FailedInstanceAffectsOnlyThatDisk()
    {
        var failed = new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["C:"] = new(PdhReadStatus.Ok, 0, 0),
            ["D:"] = new(PdhReadStatus.Failed, 0, 0xC0000BB9),
        };
        DiskIoCounterSet logical = Set(("C:", Busy), ("D:", Busy)) with { IdleTime = PdhArrayValue.Ok(failed) };

        DiskIoResult result = DiskIoMonitor.Calculate(["C:", "D:"], logical, NoPhysical);

        Assert.Equal(MonitorStatus.OK, result.Disks[0].Status);
        DiskIoItem d = result.Disks[1];
        Assert.Equal(MonitorStatus.Error, d.Status);
        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.DataError, d.ErrorCode);
        Assert.Contains(@"\LogicalDisk(*)\% Idle Time", d.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("0xC0000BB9", d.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidValues(double bad)
    {
        double[] values = [bad, 0, 0, 0, 0, 0, 50];

        DiskIoItem c = Assert.Single(DiskIoMonitor.Calculate(["C:"], Set(("C:", values)), NoPhysical).Disks);

        Assert.Equal(MonitorStatus.Error, c.Status);
        Assert.Equal(ErrorCode.DataError, c.ErrorCode);
        Assert.Contains("Disk Read Bytes/sec", c.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedArrayMarksAllDisksOfThatScope()
    {
        DiskIoCounterSet failed = Uniform(PdhArrayValue.Empty(PdhReadStatus.Failed, 0xC0000BB9));

        DiskIoResult result = DiskIoMonitor.Calculate(["C:", "D:"], failed, Set(("0 C:", Busy)));

        Assert.Equal([MonitorStatus.Error, MonitorStatus.Error, MonitorStatus.OK], result.Disks.Select(d => d.Status));
        Assert.Equal(MonitorStatus.Error, result.Status); // 任一 Error → 模組 Error（StatusAggregator 規則）
    }

    [Fact]
    public void NoDisksAndNoCountersIsUnavailable()
    {
        DiskIoResult result = DiskIoMonitor.Calculate([], Set(), Set());

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
        Assert.Empty(result.Disks);
    }

    [Fact]
    public void NoDisksWithFailedCountersIsError()
    {
        DiskIoResult result = DiskIoMonitor.Calculate([], Uniform(PdhArrayValue.Empty(PdhReadStatus.Failed, 0xC0000BB9)), Set());

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.DataError, result.ErrorCode);
    }

    [Fact]
    public async Task AddsEnglishWildcardCountersAndWarmsUp()
    {
        await _monitor.WarmUpAsync(CancellationToken.None);

        Assert.Equal(14, _pdh.AddedPaths.Count);
        Assert.Contains(@"\LogicalDisk(*)\Disk Read Bytes/sec", _pdh.AddedPaths);
        Assert.Contains(@"\LogicalDisk(*)\% Idle Time", _pdh.AddedPaths);
        Assert.Contains(@"\PhysicalDisk(*)\Avg. Disk sec/Write", _pdh.AddedPaths);
        Assert.Equal(1, _pdh.Collects);
    }

    [Fact]
    public async Task SamplesFromPdhAfterWarmUp()
    {
        _drives.Drives = ["C:"];
        DiskIoCounterSet logical = Set(("C:", Busy), ("_Total", Busy));
        DiskIoCounterSet physical = Set(("0 C:", Busy), ("_Total", Busy));
        _pdh.ArrayValues = path =>
        {
            DiskIoCounterSet set = path.StartsWith(@"\LogicalDisk", StringComparison.Ordinal) ? logical : physical;
            int index = DiskIoCounterSet.CounterNames.ToList().FindIndex(n => path.EndsWith(@"\" + n, StringComparison.Ordinal));
            return set.All[index];
        };

        await _monitor.WarmUpAsync(CancellationToken.None);
        var result = Assert.IsType<DiskIoResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(["C:", "PhysicalDisk(0 C:)"], result.Disks.Select(d => d.DeviceId));
        Assert.Equal(1, _pdh.Created);
    }

    [Fact]
    public async Task DriveListFailureFallsBackToLetterInstances()
    {
        _drives.Throws = new IOException("busy");
        _pdh.ArrayValues = path => path.StartsWith(@"\LogicalDisk", StringComparison.Ordinal)
            ? PdhArrayValue.Ok(new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase) { ["D:"] = new(PdhReadStatus.Ok, 50, 0) })
            : PdhArrayValue.Ok(new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase));

        var result = Assert.IsType<DiskIoResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(["D:"], result.Disks.Select(d => d.Disk));
    }

    [Fact]
    public async Task PdhFailureRecreatesQueryNextTime()
    {
        await _monitor.WarmUpAsync(CancellationToken.None);
        _pdh.CollectThrows = new PdhException("PdhCollectQueryData", 0xC0000BB9);

        var failed = Assert.IsType<DiskIoResult>(await _monitor.SampleAsync(CancellationToken.None));
        var next = Assert.IsType<DiskIoResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Error, failed.Status);
        Assert.Equal(ErrorCode.TransientError, failed.ErrorCode);
        Assert.Equal(1, _pdh.Disposed);
        Assert.Equal(2, _pdh.Created);
        Assert.Equal(MonitorStatus.WarmingUp, next.Status);
    }

    [Fact]
    public async Task AddCounterFailureDisposesQueryAndReportsError()
    {
        _pdh.AddArrayThrowsFor = @"\PhysicalDisk(*)\Disk Reads/sec";

        var result = Assert.IsType<DiskIoResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.TransientError, result.ErrorCode);
        Assert.Equal(1, _pdh.Disposed);
    }

    [Fact]
    public async Task MissingPdhDllIsUnavailable()
    {
        _pdh.CreateThrows = new DllNotFoundException("pdh.dll");

        var result = Assert.IsType<DiskIoResult>(await _monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
    }

    [Fact]
    public void FollowsSettings()
    {
        var disabled = new DiskIoMonitor(new AppSettings { DiskIo = new ModuleSettings { Enabled = false } }, _pdh, _drives);

        Assert.False(disabled.IsEnabled);
        Assert.True(_monitor.IsEnabled);
        Assert.Equal(ModuleNames.DiskIo, _monitor.Name);
        Assert.Contains("PhysicalDisk", _monitor.SourceDescription, StringComparison.Ordinal);
    }
}
