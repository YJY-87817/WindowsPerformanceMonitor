using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PerformanceMonitor.Application;
using PerformanceMonitor.Application.Services;
using PerformanceMonitor.Infrastructure;

namespace PerformanceMonitor.Cli;

/// <summary>
/// 主控台進入點。結束碼：0 成功、1 檢查失敗、2 參數錯誤或功能尚未提供。
/// </summary>
internal static class Program
{
    private const string Usage = """
        用法：PerformanceMonitor.Cli <命令>

          --self-check   建立服務並執行與硬體無關的基本檢查
          --version      顯示版本
          --help         顯示本說明
        """;

    private static int Main(string[] args)
    {
        string command = args.Length == 1 ? args[0] : "--help";
        switch (command)
        {
            case "--version":
                Console.WriteLine(GetVersion());
                return 0;
            case "--self-check":
                return RunSelfCheck();
            case "--help":
                Console.WriteLine(Usage);
                return args.Length == 1 ? 0 : 2;
            default:
                Console.Error.WriteLine($"不支援的參數：{command}");
                Console.Error.WriteLine(Usage);
                return 2;
        }
    }

    private static int RunSelfCheck()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddPerformanceMonitorApplication()
            .AddPerformanceMonitorInfrastructure();
        using IHost host = builder.Build();

        SelfCheckResult result = host.Services.GetRequiredService<SelfCheckService>().Run();
        Console.WriteLine($"Version: {GetVersion()}");
        Console.WriteLine($"CheckedAtUtc: {result.CheckedAtUtc:yyyy-MM-ddTHH:mm:ss.fffZ}");
        Console.WriteLine($"ClockIsMonotonic: {result.ClockIsMonotonic}");
        Console.WriteLine(result.Passed ? "Self-check: OK" : "Self-check: FAILED");
        return result.Passed ? 0 : 1;
    }

    private static string GetVersion() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
