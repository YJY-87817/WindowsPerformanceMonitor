using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>網路結果。</summary>
/// <param name="Status">模組整體狀態。</param>
/// <param name="Adapters">納入統計的介面卡。</param>
/// <param name="TotalDownloadMbps">狀態為 OK 的介面卡下載合計；沒有時為 null。</param>
/// <param name="TotalUploadMbps">上傳合計。</param>
public sealed record NetworkResult(
    MonitorStatus Status,
    IReadOnlyList<NetworkAdapterItem> Adapters,
    double? TotalDownloadMbps,
    double? TotalUploadMbps) : ModuleResult(ModuleNames.Network, Status), IDeviceResult
{
    /// <inheritdoc />
    public IEnumerable<DeviceItem> Devices => Adapters;
}

/// <summary>單一介面卡（速度為十進位 Mbps）。</summary>
/// <param name="Status">狀態；暖機中為 WarmingUp。</param>
/// <param name="AdapterName">名稱。</param>
/// <param name="InterfaceDescription">描述。</param>
/// <param name="InterfaceId">穩定識別值。</param>
/// <param name="IsPhysical">是否實體介面卡；無法判斷為 null。</param>
/// <param name="ReceivedBytes">累積接收位元組。</param>
/// <param name="SentBytes">累積傳送位元組。</param>
/// <param name="DownloadMbps">下載速度。</param>
/// <param name="UploadMbps">上傳速度。</param>
public sealed record NetworkAdapterItem(
    MonitorStatus Status,
    string AdapterName,
    string? InterfaceDescription,
    string? InterfaceId,
    bool? IsPhysical,
    long? ReceivedBytes,
    long? SentBytes,
    double? DownloadMbps,
    double? UploadMbps) : DeviceItem(Status)
{
    /// <inheritdoc />
    public override string DeviceId => AdapterName;
}
