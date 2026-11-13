using System.Text.Json.Serialization;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>磁碟容量結果。</summary>
/// <param name="Status">模組整體狀態。</param>
/// <param name="Disks">各固定磁碟。</param>
public sealed record DiskResult(MonitorStatus Status, IReadOnlyList<DiskItem> Disks) : ModuleResult(ModuleNames.Disk, Status), IDeviceResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IEnumerable<DeviceItem> Devices => Disks;
}

/// <summary>單一磁碟容量（GB 為 1024 進位）。</summary>
/// <param name="Status">狀態。</param>
/// <param name="Drive">磁碟代號，例如 <c>C:</c>。</param>
/// <param name="VolumeLabel">磁碟標籤，可空白。</param>
/// <param name="TotalGB">總容量。</param>
/// <param name="UsedGB">已使用。</param>
/// <param name="FreeGB">可用。</param>
/// <param name="UsedPercent">使用率。</param>
/// <param name="FreePercent">可用百分比。</param>
public sealed record DiskItem(
    MonitorStatus Status,
    string Drive,
    string? VolumeLabel,
    double? TotalGB,
    double? UsedGB,
    double? FreeGB,
    double? UsedPercent,
    double? FreePercent) : DeviceItem(Status)
{
    /// <inheritdoc />
    public override string DeviceId => Drive;
}
