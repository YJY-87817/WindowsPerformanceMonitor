using System.Text.Json.Serialization;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>ACPI 熱區溫度結果（不是 CPU 核心溫度）。</summary>
/// <param name="Status">模組整體狀態。</param>
/// <param name="Sensors">各熱區。</param>
public sealed record TemperatureResult(MonitorStatus Status, IReadOnlyList<ThermalZoneItem> Sensors)
    : ModuleResult(ModuleNames.Temperature, Status), IDeviceResult
{
    /// <inheritdoc />
    [JsonIgnore]
    public IEnumerable<DeviceItem> Devices => Sensors;
}

/// <summary>單一 ACPI 熱區。</summary>
/// <param name="Status">狀態。</param>
/// <param name="SensorName">熱區名稱。</param>
/// <param name="TemperatureC">溫度（°C）。</param>
public sealed record ThermalZoneItem(MonitorStatus Status, string SensorName, double? TemperatureC) : DeviceItem(Status)
{
    /// <inheritdoc />
    public override string DeviceId => SensorName;
}
