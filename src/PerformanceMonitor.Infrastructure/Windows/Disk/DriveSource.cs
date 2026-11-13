namespace PerformanceMonitor.Infrastructure.Windows.Disk;

/// <summary>單一磁碟讀數。</summary>
/// <param name="Drive">磁碟代號，例如 <c>C:</c>。</param>
/// <param name="VolumeLabel">標籤；無法讀取時為 null。</param>
/// <param name="TotalBytes">總容量；無法讀取時為 null。</param>
/// <param name="FreeBytes">可用空間（不受使用者配額限制）；無法讀取時為 null。</param>
/// <param name="Error">讀取該磁碟時的錯誤訊息；成功時為 null。</param>
public sealed record DriveReading(string Drive, string? VolumeLabel, long? TotalBytes, long? FreeBytes, string? Error = null);

/// <summary>列出本機固定磁碟（測試以假物件取代）。</summary>
public interface IDriveSource
{
    /// <summary>列出固定磁碟；列舉本身失敗時擲出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>。</summary>
    IReadOnlyList<DriveReading> GetFixedDrives();
}

/// <summary>以 <see cref="DriveInfo"/> 實作；只列 <see cref="DriveType.Fixed"/>（網路與卸除式磁碟不在範圍）。</summary>
public sealed class DriveInfoSource : IDriveSource
{
    /// <inheritdoc />
    public IReadOnlyList<DriveReading> GetFixedDrives() =>
        [.. DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(Read)];

    private static DriveReading Read(DriveInfo drive)
    {
        string name = drive.Name.TrimEnd('\\', '/');
        try
        {
            if (!drive.IsReady)
            {
                return new DriveReading(name, null, null, null, "磁碟尚未就緒");
            }

            return new DriveReading(name, drive.VolumeLabel, drive.TotalSize, drive.TotalFreeSpace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DriveReading(name, null, null, null, ex.Message);
        }
    }
}
