using System.Text.Json.Serialization;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>磁碟 I/O 結果。</summary>
/// <param name="Status">模組整體狀態。</param>
/// <param name="Disks">各邏輯磁碟。</param>
public sealed record DiskIoResult(MonitorStatus Status, IReadOnlyList<DiskIoItem> Disks) : ModuleResult(ModuleNames.DiskIo, Status), IDeviceResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IEnumerable<DeviceItem> Devices => Disks;
}

/// <summary>單一邏輯磁碟 I/O；延遲為計數器層級平均服務時間。</summary>
/// <param name="Status">狀態；第一次讀取為 WarmingUp。</param>
/// <param name="Drive">磁碟代號。</param>
/// <param name="ReadMBps">讀取 MB/s（1,048,576 bytes）。</param>
/// <param name="WriteMBps">寫入 MB/s。</param>
/// <param name="ReadIops">每秒讀取次數。</param>
/// <param name="WriteIops">每秒寫入次數。</param>
/// <param name="ReadLatencyMs">平均讀取延遲（ms）。</param>
/// <param name="WriteLatencyMs">平均寫入延遲（ms）。</param>
/// <param name="BusyPercent">忙碌率（100 − 閒置率，0～100）。</param>
public sealed record DiskIoItem(
    MonitorStatus Status,
    string Drive,
    double? ReadMBps,
    double? WriteMBps,
    double? ReadIops,
    double? WriteIops,
    double? ReadLatencyMs,
    double? WriteLatencyMs,
    double? BusyPercent) : DeviceItem(Status)
{
    /// <inheritdoc />
    public override string DeviceId => Drive;
}
