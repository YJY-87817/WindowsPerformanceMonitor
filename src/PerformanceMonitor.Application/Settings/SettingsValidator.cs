using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Settings;

/// <summary>
/// 設定值範圍與相互關係檢查（舊規格 v1.5 §9.1，依新版精簡）。回傳所有錯誤，不在第一個錯誤停止。
/// </summary>
public static class SettingsValidator
{
    /// <summary>時間設定上限（秒）：1 天。</summary>
    public const int MaxDurationSeconds = 86_400;

    /// <summary>檢查設定，回傳錯誤清單；沒有錯誤時為空。</summary>
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();

        Positive(errors, "monitor.refreshSeconds", settings.Monitor.RefreshSeconds);
        Positive(errors, "monitor.sampleSeconds", settings.Monitor.SampleSeconds);
        Positive(errors, "monitor.logSeconds", settings.Monitor.LogSeconds);
        Positive(errors, "process.logIntervalSeconds", settings.Process.LogIntervalSeconds);
        Range(errors, "process.topCount", settings.Process.TopCount, 1, 20);
        Range(errors, "timeouts.gpuSeconds", settings.Timeouts.GpuSeconds, 1, 30);

        NoBlankNames(errors, "network.includeNames", settings.Network.IncludeNames);
        NoBlankNames(errors, "network.excludeNames", settings.Network.ExcludeNames);

        ValidateThresholds(errors, settings.Thresholds);

        Range(errors, "alert.durationSeconds", settings.Alert.DurationSeconds, 0, MaxDurationSeconds);
        Range(errors, "alert.cooldownSeconds", settings.Alert.CooldownSeconds, 0, MaxDurationSeconds);
        Range(errors, "alert.recoveryDurationSeconds", settings.Alert.RecoveryDurationSeconds, 0, MaxDurationSeconds);

        DirectoryPath(errors, "logging.dataDirectory", settings.Logging.DataDirectory);
        DirectoryPath(errors, "logging.logDirectory", settings.Logging.LogDirectory);
        Range(errors, "logging.lockedFileRetryCount", settings.Logging.LockedFileRetryCount, 0, 5);

        return errors;
    }

    private static void ValidateThresholds(List<string> errors, ThresholdSettings t)
    {
        Percent(errors, "thresholds.cpuPercent", t.CpuPercent);
        Percent(errors, "thresholds.cpuRecoveryPercent", t.CpuRecoveryPercent);
        Percent(errors, "thresholds.ramPercent", t.RamPercent);
        Percent(errors, "thresholds.ramRecoveryPercent", t.RamRecoveryPercent);
        Percent(errors, "thresholds.diskFreePercent", t.DiskFreePercent);
        Percent(errors, "thresholds.diskRecoveryFreePercent", t.DiskRecoveryFreePercent);

        if (t.CpuRecoveryPercent >= t.CpuPercent)
        {
            errors.Add("thresholds.cpuRecoveryPercent：必須小於 cpuPercent。");
        }

        if (t.RamRecoveryPercent >= t.RamPercent)
        {
            errors.Add("thresholds.ramRecoveryPercent：必須小於 ramPercent。");
        }

        if (t.DiskRecoveryFreePercent <= t.DiskFreePercent)
        {
            errors.Add("thresholds.diskRecoveryFreePercent：必須大於 diskFreePercent。");
        }

        if (t.DiskFreeGB.HasValue != t.DiskRecoveryFreeGB.HasValue)
        {
            errors.Add("thresholds.diskFreeGB 與 diskRecoveryFreeGB：必須同時設定或同時為 null。");
        }
        else if (t.DiskFreeGB is double free && t.DiskRecoveryFreeGB is double recovery)
        {
            if (free < 0)
            {
                errors.Add($"thresholds.diskFreeGB：不可小於 0（目前是 {free}）。");
            }

            if (recovery <= free)
            {
                errors.Add("thresholds.diskRecoveryFreeGB：必須大於 diskFreeGB。");
            }
        }
    }

    private static void Positive(List<string> errors, string field, int value)
    {
        if (value <= 0)
        {
            errors.Add($"{field}：必須大於 0（目前是 {value}）。");
        }
    }

    private static void Range(List<string> errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors.Add($"{field}：必須介於 {min}～{max}（目前是 {value}）。");
        }
    }

    private static void Percent(List<string> errors, string field, double value)
    {
        if (double.IsNaN(value) || value < 0 || value > 100)
        {
            errors.Add($"{field}：必須介於 0～100（目前是 {value}）。");
        }
    }

    private static void NoBlankNames(List<string> errors, string field, IReadOnlyList<string> names)
    {
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{field}：不可包含空白字串。");
        }
    }

    private static void DirectoryPath(List<string> errors, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errors.Add($"{field}：不可空白。");
            return;
        }

        for (int i = 0; i < path.Length; i++)
        {
            char c = path[i];
            bool driveColon = c == ':' && i == 1 && char.IsAsciiLetter(path[0]);
            if (c < ' ' || "<>\"|?*".Contains(c, StringComparison.Ordinal) || (c == ':' && !driveColon))
            {
                errors.Add($"{field}：含有路徑不允許的字元（'{path}'）。");
                return;
            }
        }
    }
}
