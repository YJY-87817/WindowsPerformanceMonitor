using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Infrastructure.Common;
using PerformanceMonitor.Infrastructure.Logging;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Infrastructure.Storage.Csv;

namespace PerformanceMonitor.Infrastructure;

/// <summary>
/// 註冊 Infrastructure 層實作。由 App／Cli 的組裝點在 Application 之後呼叫。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>加入 Infrastructure 層實作。</summary>
    public static IServiceCollection AddPerformanceMonitorInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IHostInfo, HostInfo>();
        services.TryAddSingleton(_ => AppDataPaths.CreateDefault());
        services.AddSingleton<IAppLog, DailyLogWriter>();
        services.AddSingleton<CsvFileWriter>();
        services.Replace(ServiceDescriptor.Singleton<ISnapshotRecorder, CsvSnapshotRecorder>());
        return services;
    }
}
