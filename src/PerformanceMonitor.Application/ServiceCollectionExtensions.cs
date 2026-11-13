using Microsoft.Extensions.DependencyInjection;
using PerformanceMonitor.Application.Services;

namespace PerformanceMonitor.Application;

/// <summary>
/// 註冊 Application 層服務。由 App／Cli 的組裝點呼叫。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>加入 Application 層服務。</summary>
    public static IServiceCollection AddPerformanceMonitorApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<SelfCheckService>();
        return services;
    }
}
