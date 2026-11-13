using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>CPU 結果。</summary>
/// <param name="Status">狀態。</param>
/// <param name="UsagePercent">總使用率（%）；不可用或暖機中為 null。</param>
public sealed record CpuResult(MonitorStatus Status, double? UsagePercent) : ModuleResult(ModuleNames.Cpu, Status);
