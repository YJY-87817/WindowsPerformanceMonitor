using System.Globalization;
using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Logging;

/// <summary>啟動時寫入 Application Log 的資訊。</summary>
/// <param name="Version">程式版本。</param>
/// <param name="Settings">設定讀取結果。</param>
/// <param name="DataDirectory">CSV 目錄完整路徑。</param>
/// <param name="LogDirectory">Log 目錄完整路徑。</param>
/// <param name="Sources">各資料來源說明（例如 CPU 來源、網路篩選結果）。</param>
public sealed record StartupInfo(string Version, SettingsLoadResult Settings, string DataDirectory, string LogDirectory, IReadOnlyList<string> Sources);

/// <summary>
/// 啟動 Log（舊規格 v1.5 §5.6）：版本、設定來源與警告、週期、模組啟用狀態、告警門檻、CSV／Log 目錄。
/// 以及各資料來源說明（CPU 來源、網路介面卡篩選結果等）。
/// </summary>
public static class StartupLog
{
    private const string Source = "Startup";

    /// <summary>寫入啟動資訊。</summary>
    public static void Write(IAppLog log, StartupInfo info)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(info);
        AppSettings s = info.Settings.Settings;

        log.Info(Source, $"Windows Performance Monitor {info.Version} 啟動");
        log.Info(Source, $"設定來源：{info.Settings.Source}");
        foreach (string warning in info.Settings.Warnings)
        {
            log.Warn(Source, warning);
        }

        if (info.Settings.DefaultedFields.Count > 0)
        {
            log.Info(Source, $"使用預設值的欄位：{string.Join("、", info.Settings.DefaultedFields)}");
        }

        log.Info(Source, Invariant($"週期：取樣 {s.Monitor.SampleSeconds} 秒、畫面 {s.Monitor.RefreshSeconds} 秒、CSV {s.Monitor.LogSeconds} 秒、程序紀錄 {s.Process.LogIntervalSeconds} 秒"));
        log.Info(Source, "模組：" + string.Join("、", Modules(s).Select(m => $"{m.Name} {(m.Enabled ? "啟用" : "停用")}")));

        ThresholdSettings t = s.Thresholds;
        string diskGb = t.DiskFreeGB is double gb ? Invariant($"、可用 ≤ {gb} GB（恢復 ≥ {t.DiskRecoveryFreeGB} GB），條件 {t.DiskConditionMode}") : string.Empty;
        log.Info(Source, s.Alert.Enabled
            ? Invariant($"告警：CPU ≥ {t.CpuPercent}%（恢復 ≤ {t.CpuRecoveryPercent}%）、RAM ≥ {t.RamPercent}%（恢復 ≤ {t.RamRecoveryPercent}%）、磁碟可用 ≤ {t.DiskFreePercent}%（恢復 ≥ {t.DiskRecoveryFreePercent}%）{diskGb}；持續 {s.Alert.DurationSeconds} 秒、冷卻 {s.Alert.CooldownSeconds} 秒、恢復 {s.Alert.RecoveryDurationSeconds} 秒")
            : "告警：停用");

        log.Info(Source, s.Logging.Enabled ? $"CSV 目錄：{info.DataDirectory}" : "CSV：停用（logging.enabled = false）");
        log.Info(Source, $"Log 目錄：{info.LogDirectory}");
        foreach (string source in info.Sources)
        {
            log.Info(Source, source);
        }
    }

    private static (string Name, bool Enabled)[] Modules(AppSettings s) =>
    [
        ("CPU", s.Cpu.Enabled), ("Memory", s.Memory.Enabled), ("Disk", s.Disk.Enabled), ("DiskIo", s.DiskIo.Enabled),
        ("GPU", s.Gpu.Enabled), ("Network", s.Network.Enabled), ("Process", s.Process.Enabled), ("Temperature", s.Temperature.Enabled),
    ];

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
