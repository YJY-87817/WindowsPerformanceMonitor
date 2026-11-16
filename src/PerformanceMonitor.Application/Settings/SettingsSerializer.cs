using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Settings;

/// <summary>
/// 把設定寫成完整的 settings.json（欄位與順序同 docs/settings.example.json，規格 v2.0 §7.2）。
/// WPF 設定頁儲存時使用（Phase 3c 確認：完整改寫）；寫出的內容可由 <see cref="SettingsParser"/> 讀回相同設定。
/// </summary>
public static class SettingsSerializer
{
    /// <summary>序列化為縮排 2 格的 JSON（UTF-8 文字，中文不跳脫）。</summary>
    public static string ToJson(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        }))
        {
            w.WriteStartObject();

            w.WriteStartObject("monitor");
            w.WriteNumber("refreshSeconds", settings.Monitor.RefreshSeconds);
            w.WriteNumber("sampleSeconds", settings.Monitor.SampleSeconds);
            w.WriteNumber("logSeconds", settings.Monitor.LogSeconds);
            w.WriteEndObject();

            Module(w, "cpu", settings.Cpu);
            Module(w, "memory", settings.Memory);
            Module(w, "disk", settings.Disk);
            Module(w, "diskIo", settings.DiskIo);
            Module(w, "gpu", settings.Gpu);

            NetworkSettings n = settings.Network;
            w.WriteStartObject("network");
            w.WriteBoolean("enabled", n.Enabled);
            w.WriteBoolean("physicalOnlyByDefault", n.PhysicalOnlyByDefault);
            Strings(w, "includeNames", n.IncludeNames);
            Strings(w, "excludeNames", n.ExcludeNames);
            w.WriteBoolean("aggregateIncludedAdaptersOnly", n.AggregateIncludedAdaptersOnly);
            w.WriteEndObject();

            ProcessSettings p = settings.Process;
            w.WriteStartObject("process");
            w.WriteBoolean("enabled", p.Enabled);
            w.WriteNumber("topCount", p.TopCount);
            w.WriteBoolean("loggingEnabled", p.LoggingEnabled);
            w.WriteNumber("logIntervalSeconds", p.LogIntervalSeconds);
            w.WriteEndObject();

            Module(w, "temperature", settings.Temperature);

            ThresholdSettings t = settings.Thresholds;
            w.WriteStartObject("thresholds");
            w.WriteNumber("cpuPercent", t.CpuPercent);
            w.WriteNumber("cpuRecoveryPercent", t.CpuRecoveryPercent);
            w.WriteNumber("ramPercent", t.RamPercent);
            w.WriteNumber("ramRecoveryPercent", t.RamRecoveryPercent);
            w.WriteNumber("diskFreePercent", t.DiskFreePercent);
            w.WriteNumber("diskRecoveryFreePercent", t.DiskRecoveryFreePercent);
            NullableNumber(w, "diskFreeGB", t.DiskFreeGB);
            NullableNumber(w, "diskRecoveryFreeGB", t.DiskRecoveryFreeGB);
            w.WriteString("diskConditionMode", t.DiskConditionMode == DiskConditionMode.All ? "all" : "any");
            w.WriteEndObject();

            AlertSettings a = settings.Alert;
            w.WriteStartObject("alert");
            w.WriteBoolean("enabled", a.Enabled);
            w.WriteNumber("durationSeconds", a.DurationSeconds);
            w.WriteNumber("cooldownSeconds", a.CooldownSeconds);
            w.WriteNumber("recoveryDurationSeconds", a.RecoveryDurationSeconds);
            w.WriteBoolean("repeatNotificationAfterCooldown", a.RepeatNotificationAfterCooldown);
            w.WriteEndObject();

            w.WriteStartObject("timeouts");
            w.WriteNumber("gpuSeconds", settings.Timeouts.GpuSeconds);
            w.WriteEndObject();

            LoggingSettings l = settings.Logging;
            w.WriteStartObject("logging");
            w.WriteBoolean("enabled", l.Enabled);
            w.WriteString("dataDirectory", l.DataDirectory);
            w.WriteString("logDirectory", l.LogDirectory);
            w.WriteNumber("lockedFileRetryCount", l.LockedFileRetryCount);
            w.WriteEndObject();

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void Module(Utf8JsonWriter w, string name, ModuleSettings module)
    {
        w.WriteStartObject(name);
        w.WriteBoolean("enabled", module.Enabled);
        w.WriteEndObject();
    }

    private static void Strings(Utf8JsonWriter w, string name, IReadOnlyList<string> values)
    {
        w.WriteStartArray(name);
        foreach (string value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    }

    private static void NullableNumber(Utf8JsonWriter w, string name, double? value)
    {
        if (value is double v)
        {
            w.WriteNumber(name, v);
        }
        else
        {
            w.WriteNull(name);
        }
    }
}
