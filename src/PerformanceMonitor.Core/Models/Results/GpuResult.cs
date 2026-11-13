using System.Globalization;
using System.Text.Json.Serialization;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>NVIDIA GPU 結果。</summary>
/// <param name="Status">模組整體狀態。</param>
/// <param name="Gpus">各 GPU。</param>
public sealed record GpuResult(MonitorStatus Status, IReadOnlyList<GpuItem> Gpus) : ModuleResult(ModuleNames.Gpu, Status), IDeviceResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IEnumerable<DeviceItem> Devices => Gpus;
}

/// <summary>單一 GPU。溫度寫入 TemperatureMetrics（SensorType = Gpu）。</summary>
/// <param name="Status">狀態。</param>
/// <param name="GpuIndex">nvidia-smi index。</param>
/// <param name="GpuName">名稱。</param>
/// <param name="UsagePercent">使用率。</param>
/// <param name="MemoryUsedMB">已使用顯存（MB）。</param>
/// <param name="MemoryTotalMB">顯存總量（MB）。</param>
/// <param name="TemperatureC">溫度（°C）；不支援時為 null。</param>
public sealed record GpuItem(
    MonitorStatus Status,
    int GpuIndex,
    string GpuName,
    double? UsagePercent,
    double? MemoryUsedMB,
    double? MemoryTotalMB,
    double? TemperatureC) : DeviceItem(Status)
{
    /// <inheritdoc />
    public override string DeviceId => string.Create(CultureInfo.InvariantCulture, $"GPU {GpuIndex}");
}
