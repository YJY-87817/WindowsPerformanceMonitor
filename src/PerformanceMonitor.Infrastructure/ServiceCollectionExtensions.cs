using Microsoft.Extensions.DependencyInjection;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Infrastructure.Common;

namespace PerformanceMonitor.Infrastructure;

/// <summary>
/// 註冊 Infrastructure 層實作。由 App／Cli 的組裝點呼叫。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>加入 Infrastructure 層實作。</summary>
    public static IServiceCollection AddPerformanceMonitorInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
