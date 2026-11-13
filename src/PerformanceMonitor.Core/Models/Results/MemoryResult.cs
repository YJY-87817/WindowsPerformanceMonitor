using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>記憶體結果（GB 為 1024 進位）。</summary>
/// <param name="Status">狀態。</param>
/// <param name="TotalGB">總記憶體。</param>
/// <param name="UsedGB">已使用。</param>
/// <param name="FreeGB">可用。</param>
/// <param name="UsagePercent">使用率（%）。</param>
public sealed record MemoryResult(MonitorStatus Status, double? TotalGB, double? UsedGB, double? FreeGB, double? UsagePercent)
    : ModuleResult(ModuleNames.Memory, Status);
