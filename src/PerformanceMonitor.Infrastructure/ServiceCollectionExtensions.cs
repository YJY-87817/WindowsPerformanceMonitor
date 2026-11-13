using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Infrastructure.Common;
using PerformanceMonitor.Infrastructure.Logging;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Infrastructure.Storage.Csv;
using PerformanceMonitor.Infrastructure.Windows.Memory;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure;

/// <summary>
/// 註冊 Infrastructure 層實作與監控模組。由 App／Cli 的組裝點在 Application 之後呼叫；
/// 組裝點另須註冊讀取並驗證後的 <see cref="Core.Settings.AppSettings"/>。
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

        // 監控模組：註冊順序即取樣與輸出順序。
        services.AddSingleton<IPdhQueryFactory, PdhQueryFactory>();
        services.AddSingleton<IMemoryStatusSource, GlobalMemoryStatusSource>();
        services.AddSingleton<IMonitor, CpuMonitor>();
        services.AddSingleton<IMonitor, MemoryMonitor>();
        return services;
    }
}
