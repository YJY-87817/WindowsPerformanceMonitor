using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PerformanceMonitor.Application.Alerts;
using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Application.Services;
using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Application;

/// <summary>
/// 註冊 Application 層服務。由 App／Cli 的組裝點呼叫。
/// 組裝點另須註冊 <see cref="Core.Settings.AppSettings"/>（讀取並驗證後的設定）與各 <see cref="IMonitor"/>。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>加入 Application 層服務。</summary>
    public static IServiceCollection AddPerformanceMonitorApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<SelfCheckService>();
        services.AddSingleton<SampleRunner>();
        services.AddSingleton<MonitoringEngine>();
        services.AddSingleton<MonitoringSession>();
        services.AddSingleton<IMonitoringSession>(sp => sp.GetRequiredService<MonitoringSession>());
        services.AddSingleton<MonitoringController>();
        services.AddSingleton<SnapshotStore>();
        services.AddSingleton<ISnapshotProvider>(sp => sp.GetRequiredService<SnapshotStore>());
        services.AddSingleton<StorageStatus>();
        services.AddSingleton<IStorageStatus>(sp => sp.GetRequiredService<StorageStatus>());
        services.AddSingleton<StatusLogTracker>();
        services.AddSingleton<MonitoringLogObserver>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<IAlertProvider>(sp => sp.GetRequiredService<AlertService>());
        services.TryAddSingleton<ISnapshotRecorder, NullSnapshotRecorder>();
        return services;
    }
}
