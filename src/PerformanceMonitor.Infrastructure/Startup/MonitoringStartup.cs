using Microsoft.Extensions.DependencyInjection;
using PerformanceMonitor.Application.Logging;
using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Settings;
using PerformanceMonitor.Infrastructure.Storage;

namespace PerformanceMonitor.Infrastructure.Startup;

/// <summary>啟動前的設定與資料目錄檢查結果。</summary>
/// <param name="Settings">設定讀取結果。</param>
/// <param name="StorageErrors">資料目錄無法寫入的原因；設定有錯誤時不檢查（為空）。</param>
public sealed record StartupCheck(SettingsLoadResult Settings, IReadOnlyList<string> StorageErrors)
{
    /// <summary>可以開始監控：設定與目錄都沒有錯誤。</summary>
    public bool CanMonitor => Settings.IsValid && StorageErrors.Count == 0;

    /// <summary>要註冊到 DI 的設定；設定有錯誤時為內建預設值（不開始監控，只讓服務可以建立）。</summary>
    public AppSettings EffectiveSettings => Settings.IsValid ? Settings.Settings : new AppSettings();

    /// <summary>所有錯誤（設定錯誤附設定來源；目錄錯誤附處理方式）。</summary>
    public IReadOnlyList<string> Errors =>
    [
        .. Settings.Errors.Select(e => $"設定檔（{Settings.Source}）：{e}"),
        .. StorageErrors.Select(e => $"資料目錄無法寫入（請修改 logging.dataDirectory／logDirectory）：{e}"),
    ];
}

/// <summary>主控台與 WPF 共用的啟動步驟：讀取設定 → 檢查資料目錄 →（組裝 DI 後）寫啟動 Log。</summary>
public static class MonitoringStartup
{
    /// <summary>讀取設定並檢查資料目錄（規格 v2.0 §5、§7.1）。</summary>
    public static StartupCheck Check(AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        SettingsLoadResult settings = SettingsFileLoader.Load(paths.SettingsFile);
        IReadOnlyList<string> storageErrors = settings.IsValid ? StorageInitializer.Check(settings.Settings, paths) : [];
        return new StartupCheck(settings, storageErrors);
    }

    /// <summary>寫啟動 Log（版本、設定、目錄、各資料來源說明）。只在可以開始監控時呼叫。</summary>
    public static void WriteStartupLog(IServiceProvider services, string version, StartupCheck check, AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(paths);
        IAppLog log = services.GetRequiredService<IAppLog>();
        string[] sources = [.. services.GetServices<IMonitor>().Select(m => m.SourceDescription).OfType<string>()];
        StartupLog.Write(log, new StartupInfo(
            version,
            check.Settings,
            paths.ResolveDataDirectory(check.Settings.Settings.Logging),
            paths.ResolveLogDirectory(check.Settings.Settings.Logging),
            sources));
    }
}
