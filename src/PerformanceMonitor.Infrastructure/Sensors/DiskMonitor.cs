using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Disk;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>
/// 固定磁碟容量：<see cref="DriveInfo"/>（不需 WMI）。計算沿用舊規格 v1.5 §3.4：
/// 已使用 = 總量 − 可用；GB 為 1024 進位；使用率與可用百分比。
/// 單一磁碟容量為 0、無法讀取或可用大於總量時，該磁碟為 <c>Error</c>，不影響其他磁碟；
/// 沒有任何固定磁碟時模組為 <c>Unavailable</c>。以磁碟代號識別，標籤只作顯示。
/// </summary>
public sealed class DiskMonitor(AppSettings settings, IDriveSource source) : IMonitor
{
    private const double BytesPerGB = 1_073_741_824d;

    /// <inheritdoc />
    public string Name => ModuleNames.Disk;

    /// <inheritdoc />
    public bool IsEnabled => settings.Disk.Enabled;

    /// <inheritdoc />
    public string? SourceDescription => "磁碟容量來源：System.IO.DriveInfo（固定磁碟）";

    /// <inheritdoc />
    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DriveReading> drives;
        try
        {
            drives = source.GetFixedDrives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult<ModuleResult>(new DiskResult(MonitorStatus.Error, [])
            {
                ErrorCode = ErrorCode.TransientError,
                ErrorMessage = $"列舉磁碟失敗：{ex.Message}",
            });
        }

        return Task.FromResult<ModuleResult>(Calculate(drives));
    }

    /// <summary>由讀數計算結果（純函式，供測試）。</summary>
    public static DiskResult Calculate(IReadOnlyList<DriveReading> drives)
    {
        ArgumentNullException.ThrowIfNull(drives);
        if (drives.Count == 0)
        {
            return new DiskResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "沒有固定磁碟" };
        }

        DiskItem[] items = [.. drives.OrderBy(d => d.Drive, StringComparer.OrdinalIgnoreCase).Select(Item)];
        return new DiskResult(StatusAggregator.Aggregate(items.Select(i => i.Status)), items);
    }

    private static DiskItem Item(DriveReading d)
    {
        if (d.Error is not null)
        {
            return Failed(d, ErrorCode.TransientError, d.Error);
        }

        if (d.TotalBytes is not long total || total <= 0)
        {
            return Failed(d, ErrorCode.DataError, "容量為 0 或無法取得");
        }

        if (d.FreeBytes is not long free || free < 0 || free > total)
        {
            return Failed(d, ErrorCode.DataError, string.Create(CultureInfo.InvariantCulture, $"可用空間不合理（{d.FreeBytes} / {total}）"));
        }

        double used = total - free;
        return new DiskItem(
            MonitorStatus.OK,
            d.Drive,
            d.VolumeLabel,
            total / BytesPerGB,
            used / BytesPerGB,
            free / BytesPerGB,
            used / total * 100,
            (double)free / total * 100);
    }

    private static DiskItem Failed(DriveReading d, ErrorCode code, string message) =>
        new(MonitorStatus.Error, d.Drive, d.VolumeLabel, null, null, null, null, null) { ErrorCode = code, ErrorMessage = message };
}
