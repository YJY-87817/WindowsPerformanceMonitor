using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Infrastructure.Common;

/// <summary>
/// 執行電腦資訊：<see cref="Environment.MachineName"/> 與 <see cref="TimeZoneInfo.Local"/>。
/// </summary>
public sealed class HostInfo : IHostInfo
{
    /// <inheritdoc />
    public string ComputerName => Environment.MachineName;

    /// <inheritdoc />
    public TimeSpan GetUtcOffset(DateTimeOffset utcTime) => TimeZoneInfo.Local.GetUtcOffset(utcTime);
}
