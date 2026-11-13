using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PerformanceMonitor.Application;
using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Application.Output;
using PerformanceMonitor.Application.Services;
using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Infrastructure;
using PerformanceMonitor.Infrastructure.Settings;
using PerformanceMonitor.Infrastructure.Storage;

namespace PerformanceMonitor.Cli;

/// <summary>
/// 主控台進入點。結束碼：0 成功、1 檢查失敗或設定／目錄錯誤、2 參數錯誤。
/// </summary>
internal static class Program
{
    private const string Usage = """
        用法：PerformanceMonitor.Cli <命令>

          --once           取樣一次，寫入 CSV 與 Log 後結束（行為與連續監控相同）
          --once --json    同上，以 JSON 輸出完整結果（供腳本使用）
          --self-check     建立服務並執行與硬體無關的基本檢查
          --version        顯示版本
          --help           顯示本說明

        設定檔：%LOCALAPPDATA%\WindowsPerformanceMonitor\settings.json（不存在時使用內建預設值）
        """;

    private static async Task<int> Main(string[] args)
    {
        string[] normalized = [.. args.Select(a => a.ToLowerInvariant())];
        switch (normalized)
        {
            case ["--version"]:
                Console.WriteLine(GetVersion());
                return 0;
            case ["--self-check"]:
                return RunSelfCheck();
            case ["--once"]:
                return await RunOnceAsync(json: false).ConfigureAwait(false);
            case ["--once", "--json"] or ["--json", "--once"]:
                return await RunOnceAsync(json: true).ConfigureAwait(false);
            case [] or ["--help"]:
                Console.WriteLine(Usage);
                return normalized.Length == 0 ? 2 : 0;
            default:
                await Console.Error.WriteLineAsync($"不支援的參數：{string.Join(' ', args)}").ConfigureAwait(false);
                await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
                return 2;
        }
    }

    private static int RunSelfCheck()
    {
        HostApplicationBuilder builder = CreateBuilder();
        using IHost host = builder.Build();

        SelfCheckResult result = host.Services.GetRequiredService<SelfCheckService>().Run();
        Console.WriteLine($"Version: {GetVersion()}");
        Console.WriteLine($"CheckedAtUtc: {result.CheckedAtUtc:yyyy-MM-ddTHH:mm:ss.fffZ}");
        Console.WriteLine($"ClockIsMonotonic: {result.ClockIsMonotonic}");
        Console.WriteLine(result.Passed ? "Self-check: OK" : "Self-check: FAILED");
        return result.Passed ? 0 : 1;
    }

    private static async Task<int> RunOnceAsync(bool json)
    {
        AppDataPaths paths = AppDataPaths.CreateDefault();
        SettingsLoadResult settings = SettingsFileLoader.Load(paths.SettingsFile);
        foreach (string warning in settings.Warnings)
        {
            await Console.Error.WriteLineAsync($"警告：{warning}").ConfigureAwait(false);
        }

        if (!settings.IsValid)
        {
            await Console.Error.WriteLineAsync($"設定檔有錯誤，未開始監控（{settings.Source}）：").ConfigureAwait(false);
            foreach (string error in settings.Errors)
            {
                await Console.Error.WriteLineAsync($"  - {error}").ConfigureAwait(false);
            }

            return 1;
        }

        IReadOnlyList<string> storageErrors = StorageInitializer.Check(settings.Settings, paths);
        if (storageErrors.Count > 0)
        {
            await Console.Error.WriteLineAsync("資料目錄無法寫入，未開始監控（請修改設定檔的 logging.dataDirectory／logDirectory）：").ConfigureAwait(false);
            foreach (string error in storageErrors)
            {
                await Console.Error.WriteLineAsync($"  - {error}").ConfigureAwait(false);
            }

            return 1;
        }

        HostApplicationBuilder builder = CreateBuilder();
        builder.Services.AddSingleton(settings.Settings);
        builder.Services.AddSingleton(paths);
        using IHost host = builder.Build();

        IAppLog log = host.Services.GetRequiredService<IAppLog>();
        string[] sources = [.. host.Services.GetServices<IMonitor>().Select(m => m.SourceDescription).OfType<string>()];
        StartupLog.Write(log, new StartupInfo(
            GetVersion(),
            settings,
            paths.ResolveDataDirectory(settings.Settings.Logging),
            paths.ResolveLogDirectory(settings.Settings.Logging),
            sources));

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        MonitorSnapshot snapshot = await host.Services.GetRequiredService<MonitoringSession>().RunOnceAsync(cancellation.Token).ConfigureAwait(false);
        IStorageStatus storage = host.Services.GetRequiredService<IStorageStatus>();
        Console.Write(json ? SnapshotFormatter.ToJson(snapshot, storage) + "\n" : SnapshotFormatter.ToText(snapshot, storage));
        return 0;
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // 主控台只輸出結果；.NET 框架本身的 Log 不顯示（程式事件寫在專用 Log 檔）。
        builder.Logging.ClearProviders();
        builder.Services
            .AddPerformanceMonitorApplication()
            .AddPerformanceMonitorInfrastructure();
        return builder;
    }

    private static string GetVersion() =>
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
