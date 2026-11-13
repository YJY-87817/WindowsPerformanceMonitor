using PerformanceMonitor.Core.Models.Alerts;

namespace PerformanceMonitor.Core.Abstractions;

/// <summary>提供目前告警狀態給 UI。</summary>
public interface IAlertProvider
{
    /// <summary>目前的告警狀態。</summary>
    AlertView Current { get; }
}
