using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Disk;

namespace PerformanceMonitor.Infrastructure.Tests;

public class DiskMonitorTests
{
    private const long GB = 1_073_741_824;

    private sealed class FakeDrives(Func<IReadOnlyList<DriveReading>> list) : IDriveSource
    {
        public IReadOnlyList<DriveReading> GetFixedDrives() => list();
    }

    private static async Task<DiskResult> SampleAsync(Func<IReadOnlyList<DriveReading>> list) =>
        Assert.IsType<DiskResult>(await new DiskMonitor(new AppSettings(), new FakeDrives(list)).SampleAsync(CancellationToken.None));

    [Fact]
    public void CalculatesEachDiskSortedByDrive()
    {
        DiskResult result = DiskMonitor.Calculate([
            new DriveReading("D:", "Data", 1000 * GB, 500 * GB),
            new DriveReading("C:", "System", 200 * GB, 50 * GB),
        ]);

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(["C:", "D:"], result.Disks.Select(d => d.Drive));
        DiskItem c = result.Disks[0];
        Assert.Equal("System", c.VolumeLabel);
        Assert.Equal(200, c.TotalGB);
        Assert.Equal(150, c.UsedGB);
        Assert.Equal(50, c.FreeGB);
        Assert.Equal(75, c.UsedPercent);
        Assert.Equal(25, c.FreePercent);
    }

    [Theory]
    [InlineData(0L, 0L, "容量為 0")]
    [InlineData(null, 5L, "容量為 0")]
    [InlineData(100L, 101L, "可用空間不合理")]
    [InlineData(100L, null, "可用空間不合理")]
    public void InvalidCapacityMarksOnlyThatDiskAsDataError(long? totalGB, long? freeGB, string message)
    {
        DiskResult result = DiskMonitor.Calculate([
            new DriveReading("C:", null, 100 * GB, 10 * GB),
            new DriveReading("E:", null, totalGB * GB, freeGB * GB),
        ]);

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(MonitorStatus.OK, result.Disks[0].Status);
        DiskItem e = result.Disks[1];
        Assert.Equal(MonitorStatus.Error, e.Status);
        Assert.Equal(ErrorCode.DataError, e.ErrorCode);
        Assert.Contains(message, e.ErrorMessage, StringComparison.Ordinal);
        Assert.Null(e.UsedPercent);
    }

    [Fact]
    public void UnreadableDiskIsTransientError()
    {
        DiskItem item = DiskMonitor.Calculate([new DriveReading("F:", null, null, null, "磁碟尚未就緒")]).Disks[0];

        Assert.Equal(ErrorCode.TransientError, item.ErrorCode);
        Assert.Equal("磁碟尚未就緒", item.ErrorMessage);
    }

    [Fact]
    public void FullDiskIsZeroFreeNotMissing()
    {
        DiskItem item = DiskMonitor.Calculate([new DriveReading("C:", null, 100 * GB, 0)]).Disks[0];

        Assert.Equal(MonitorStatus.OK, item.Status);
        Assert.Equal(0, item.FreeGB);
        Assert.Equal(100, item.UsedPercent);
    }

    [Fact]
    public void NoFixedDisksIsUnavailable()
    {
        DiskResult result = DiskMonitor.Calculate([]);

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
    }

    [Fact]
    public async Task EnumerationFailureIsModuleTransientError()
    {
        DiskResult result = await SampleAsync(() => throw new IOException("device busy"));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Equal(ErrorCode.TransientError, result.ErrorCode);
        Assert.Empty(result.Disks);
    }

    [Fact]
    public void NameEnabledAndSource()
    {
        var monitor = new DiskMonitor(new AppSettings { Disk = new ModuleSettings { Enabled = false } }, new FakeDrives(() => []));

        Assert.Equal(ModuleNames.Disk, monitor.Name);
        Assert.False(monitor.IsEnabled);
        Assert.Contains("DriveInfo", monitor.SourceDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void RealDriveInfoSourceDoesNotThrow()
    {
        // 在 Linux 開發環境通常沒有 Fixed 磁碟；只確認列舉不會擲出例外，且代號不含結尾斜線。
        IReadOnlyList<DriveReading> drives = new DriveInfoSource().GetFixedDrives();

        Assert.All(drives, d => Assert.False(d.Drive.EndsWith('\\') || (d.Drive.Length > 1 && d.Drive.EndsWith('/'))));
    }
}
