using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PerformanceMonitor.Application;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Application.Output;
using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Application.Services;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Infrastructure;
using PerformanceMonitor.Infrastructure.Startup;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Reporting.Html;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Cli;

/// <summary>
/// 主控台進入點。結束碼：0 成功、1 檢查失敗、設定／目錄錯誤或報告寫入失敗、2 參數錯誤。
/// </summary>
internal static class Program
{
    private const string Usage = """
        用法：PerformanceMonitor.Cli <命令>

          --once           取樣一次，寫入 CSV 與 Log 後結束（行為與連續監控相同）
          --once --json    同上，以 JSON 輸出完整結果（供腳本使用）
          report [--from 日期] [--to 日期] [--out 路徑]
                           產生離線 HTML 報告（日期為本機日期 yyyy-MM-dd，含頭含尾，最長 366 天；
                           省略 --from／--to 時為今天；預設寫到資料目錄 Reports\Report_起日-訖日.html）
          --self-check     建立服務並執行與硬體無關的基本檢查
          --version        顯示版本
          --help           顯示本說明

        設定檔：%LOCALAPPDATA%\WindowsPerformanceMonitor\settings.json（不存在時使用內建預設值）
        """;

    private static async Task<int> Main(string[] args)
    {
        string[] normalized = [.. args.Select(a => a.ToLowerInvariant())];
        if (normalized is ["report", ..])
        {
            return await RunReportAsync(args[1..]).ConfigureAwait(false);
        }

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
        StartupCheck check = MonitoringStartup.Check(paths);
        foreach (string warning in check.Settings.Warnings)
        {
            await Console.Error.WriteLineAsync($"警告：{warning}").ConfigureAwait(false);
        }

        if (!check.Settings.IsValid)
        {
            await Console.Error.WriteLineAsync($"設定檔有錯誤，未開始監控（{check.Settings.Source}）：").ConfigureAwait(false);
            foreach (string error in check.Settings.Errors)
            {
                await Console.Error.WriteLineAsync($"  - {error}").ConfigureAwait(false);
            }

            return 1;
        }

        if (check.StorageErrors.Count > 0)
        {
            await Console.Error.WriteLineAsync("資料目錄無法寫入，未開始監控（請修改設定檔的 logging.dataDirectory／logDirectory）：").ConfigureAwait(false);
            foreach (string error in check.StorageErrors)
            {
                await Console.Error.WriteLineAsync($"  - {error}").ConfigureAwait(false);
            }

            return 1;
        }

        HostApplicationBuilder builder = CreateBuilder();
        builder.Services.AddSingleton(check.Settings.Settings);
        builder.Services.AddSingleton(paths);
        using IHost host = builder.Build();
        MonitoringStartup.WriteStartupLog(host.Services, GetVersion(), check, paths);

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

    private static async Task<int> RunReportAsync(string[] args)
    {
        if (!TryParseReportArgs(args, out DateOnly? from, out DateOnly? to, out string? output, out string? argError))
        {
            await Console.Error.WriteLineAsync(argError).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(Usage).ConfigureAwait(false);
            return 2;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        DateOnly end = to ?? from ?? today;
        ReportPeriod? period = ReportPeriod.TryCreate(from ?? end, end, out string? periodError);
        if (period is null)
        {
            await Console.Error.WriteLineAsync($"報告期間錯誤：{periodError}").ConfigureAwait(false);
            return 2;
        }

        AppDataPaths paths = AppDataPaths.CreateDefault();
        StartupCheck check = MonitoringStartup.Check(paths);
        if (!check.Settings.IsValid)
        {
            await Console.Error.WriteLineAsync($"設定檔有錯誤，無法找到資料目錄（{check.Settings.Source}）：").ConfigureAwait(false);
            foreach (string error in check.Settings.Errors)
            {
                await Console.Error.WriteLineAsync($"  - {error}").ConfigureAwait(false);
            }

            return 1;
        }

        HostApplicationBuilder builder = CreateBuilder();
        builder.Services.AddSingleton(check.Settings.Settings);
        builder.Services.AddSingleton(paths);
        using IHost host = builder.Build();

        ReportResult result;
        try
        {
            result = host.Services.GetRequiredService<ReportService>().Generate(period, output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            await Console.Error.WriteLineAsync($"報告寫入失敗：{ex.Message}").ConfigureAwait(false);
            return 1;
        }

        DataQuality q = result.Model.Quality;
        Console.WriteLine($"報告：{result.Path}");
        Console.WriteLine($"期間：{period}（趨勢粒度：{result.Model.Timeline.GranularityText}）");
        Console.WriteLine($"指標：{result.Model.Series.Count} 項；SystemMetrics 紀錄 {q.ActualSamples} 筆，預期 {(q.ExpectedSamples?.ToString(CultureInfo.InvariantCulture) ?? "無法計算")}，缺失 {HtmlReportRenderer.Ratio(q.MissingRatio)}");
        Console.WriteLine($"監控時段：{q.Sessions.Count} 個；檔案問題：{q.FileIssues.Count} 個；沒有資料的日期：{q.DatesWithoutData.Count} 天");
        return 0;
    }

    /// <summary>解析 report 的參數。</summary>
    private static bool TryParseReportArgs(string[] args, out DateOnly? from, out DateOnly? to, out string? output, out string? error)
    {
        from = null;
        to = null;
        output = null;
        error = null;
        for (int i = 0; i < args.Length; i++)
        {
            string name = args[i].ToLowerInvariant();
            if (i + 1 >= args.Length || name is not ("--from" or "--to" or "--out"))
            {
                error = $"不支援的 report 參數：{args[i]}";
                return false;
            }

            string value = args[++i];
            if (name == "--out")
            {
                output = value;
                continue;
            }

            if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                error = $"{name} 不是 yyyy-MM-dd 格式的日期：{value}";
                return false;
            }

            if (name == "--from")
            {
                from = date;
            }
            else
            {
                to = date;
            }
        }

        return true;
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
