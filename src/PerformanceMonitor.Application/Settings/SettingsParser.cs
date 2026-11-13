using System.Text.Json;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Settings;

/// <summary>
/// 將 settings.json 內容轉為 <see cref="AppSettings"/>：逐欄檢查型別、缺少欄位用預設值、
/// 未知欄位警告，最後執行 <see cref="SettingsValidator"/>。欄位名稱使用 camelCase、不分大小寫。
/// </summary>
public static class SettingsParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>解析設定內容。</summary>
    /// <param name="json">設定檔內容。</param>
    /// <param name="source">來源（檔案路徑），用於錯誤訊息。</param>
    public static SettingsLoadResult Parse(string json, string source)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            string position = ex.LineNumber is long line ? $"第 {line + 1} 行：" : string.Empty;
            return new SettingsLoadResult(new AppSettings(), source, [$"設定檔 '{source}' 不是有效的 JSON（{position}{ex.Message}）"], [], []);
        }

        using (document)
        {
            var context = new ReadContext();
            AppSettings settings = Read(document.RootElement, context);
            List<string> errors = [.. context.Errors];
            if (errors.Count == 0)
            {
                errors.AddRange(SettingsValidator.Validate(settings));
            }

            return new SettingsLoadResult(
                errors.Count == 0 ? settings : new AppSettings(),
                source,
                errors,
                context.Warnings,
                context.Defaulted);
        }
    }

    private static AppSettings Read(JsonElement root, ReadContext context)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            context.Errors.Add("設定檔最外層必須是 JSON 物件。");
            return new AppSettings();
        }

        var defaults = new AppSettings();
        var top = new SectionReader(root, string.Empty, context);
        AppSettings result = new()
        {
            Monitor = ReadMonitor(top.Section("monitor"), defaults.Monitor),
            Cpu = ReadModule(top.Section("cpu")),
            Memory = ReadModule(top.Section("memory")),
            Disk = ReadModule(top.Section("disk")),
            DiskIo = ReadModule(top.Section("diskIo")),
            Gpu = ReadModule(top.Section("gpu")),
            Network = ReadNetwork(top.Section("network"), defaults.Network),
            Process = ReadProcess(top.Section("process"), defaults.Process),
            Temperature = ReadModule(top.Section("temperature")),
            Thresholds = ReadThresholds(top.Section("thresholds"), defaults.Thresholds),
            Alert = ReadAlert(top.Section("alert"), defaults.Alert),
            Timeouts = ReadTimeouts(top.Section("timeouts"), defaults.Timeouts),
            Logging = ReadLogging(top.Section("logging"), defaults.Logging),
        };
        top.ReportUnknown();
        return result;
    }

    private static MonitorSettings ReadMonitor(SectionReader s, MonitorSettings d)
    {
        var result = new MonitorSettings
        {
            RefreshSeconds = s.Int("refreshSeconds", d.RefreshSeconds),
            SampleSeconds = s.Int("sampleSeconds", d.SampleSeconds),
            LogSeconds = s.Int("logSeconds", d.LogSeconds),
        };
        s.ReportUnknown();
        return result;
    }

    private static ModuleSettings ReadModule(SectionReader s)
    {
        var result = new ModuleSettings { Enabled = s.Bool("enabled", true) };
        s.ReportUnknown();
        return result;
    }

    private static NetworkSettings ReadNetwork(SectionReader s, NetworkSettings d)
    {
        var result = new NetworkSettings
        {
            Enabled = s.Bool("enabled", d.Enabled),
            PhysicalOnlyByDefault = s.Bool("physicalOnlyByDefault", d.PhysicalOnlyByDefault),
            IncludeNames = s.StringArray("includeNames", d.IncludeNames),
            ExcludeNames = s.StringArray("excludeNames", d.ExcludeNames),
            AggregateIncludedAdaptersOnly = s.Bool("aggregateIncludedAdaptersOnly", d.AggregateIncludedAdaptersOnly),
        };
        s.ReportUnknown();
        return result;
    }

    private static ProcessSettings ReadProcess(SectionReader s, ProcessSettings d)
    {
        var result = new ProcessSettings
        {
            Enabled = s.Bool("enabled", d.Enabled),
            TopCount = s.Int("topCount", d.TopCount),
            LoggingEnabled = s.Bool("loggingEnabled", d.LoggingEnabled),
            LogIntervalSeconds = s.Int("logIntervalSeconds", d.LogIntervalSeconds),
        };
        s.ReportUnknown();
        return result;
    }

    private static ThresholdSettings ReadThresholds(SectionReader s, ThresholdSettings d)
    {
        var result = new ThresholdSettings
        {
            CpuPercent = s.Number("cpuPercent", d.CpuPercent),
            CpuRecoveryPercent = s.Number("cpuRecoveryPercent", d.CpuRecoveryPercent),
            RamPercent = s.Number("ramPercent", d.RamPercent),
            RamRecoveryPercent = s.Number("ramRecoveryPercent", d.RamRecoveryPercent),
            DiskFreePercent = s.Number("diskFreePercent", d.DiskFreePercent),
            DiskRecoveryFreePercent = s.Number("diskRecoveryFreePercent", d.DiskRecoveryFreePercent),
            DiskFreeGB = s.NullableNumber("diskFreeGB", d.DiskFreeGB),
            DiskRecoveryFreeGB = s.NullableNumber("diskRecoveryFreeGB", d.DiskRecoveryFreeGB),
            DiskConditionMode = s.Choice("diskConditionMode", d.DiskConditionMode),
        };
        s.ReportUnknown();
        return result;
    }

    private static AlertSettings ReadAlert(SectionReader s, AlertSettings d)
    {
        var result = new AlertSettings
        {
            Enabled = s.Bool("enabled", d.Enabled),
            DurationSeconds = s.Int("durationSeconds", d.DurationSeconds),
            CooldownSeconds = s.Int("cooldownSeconds", d.CooldownSeconds),
            RecoveryDurationSeconds = s.Int("recoveryDurationSeconds", d.RecoveryDurationSeconds),
            RepeatNotificationAfterCooldown = s.Bool("repeatNotificationAfterCooldown", d.RepeatNotificationAfterCooldown),
        };
        s.ReportUnknown();
        return result;
    }

    private static TimeoutSettings ReadTimeouts(SectionReader s, TimeoutSettings d)
    {
        var result = new TimeoutSettings { GpuSeconds = s.Int("gpuSeconds", d.GpuSeconds) };
        s.ReportUnknown();
        return result;
    }

    private static LoggingSettings ReadLogging(SectionReader s, LoggingSettings d)
    {
        var result = new LoggingSettings
        {
            Enabled = s.Bool("enabled", d.Enabled),
            DataDirectory = s.String("dataDirectory", d.DataDirectory),
            LogDirectory = s.String("logDirectory", d.LogDirectory),
            LockedFileRetryCount = s.Int("lockedFileRetryCount", d.LockedFileRetryCount),
        };
        s.ReportUnknown();
        return result;
    }

    private sealed class ReadContext
    {
        public List<string> Errors { get; } = [];

        public List<string> Warnings { get; } = [];

        public List<string> Defaulted { get; } = [];
    }

    /// <summary>讀取一個 JSON 物件的欄位，記錄錯誤、缺少欄位與未知欄位。</summary>
    private sealed class SectionReader
    {
        private readonly JsonElement? _element;
        private readonly string _path;
        private readonly ReadContext _context;
        private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);

        public SectionReader(JsonElement? element, string path, ReadContext context)
        {
            _element = element;
            _path = path;
            _context = context;
        }

        public SectionReader Section(string name)
        {
            _known.Add(name);
            string path = Join(name);
            if (!TryGet(name, out JsonElement value))
            {
                _context.Defaulted.Add(path);
                return new SectionReader(null, path, _context);
            }

            if (value.ValueKind != JsonValueKind.Object)
            {
                _context.Errors.Add($"{path}：必須是 JSON 物件。");
                return new SectionReader(null, path, _context);
            }

            return new SectionReader(value, path, _context);
        }

        public int Int(string name, int defaultValue) =>
            Read(name, defaultValue, "整數", v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i) ? new Box<int>(i) : null);

        public double Number(string name, double defaultValue) =>
            Read(name, defaultValue, "數字", v => v.ValueKind == JsonValueKind.Number ? new Box<double>(v.GetDouble()) : null);

        public double? NullableNumber(string name, double? defaultValue) =>
            Read(name, defaultValue, "數字或 null", v => v.ValueKind switch
            {
                JsonValueKind.Null => new Box<double?>(null),
                JsonValueKind.Number => new Box<double?>(v.GetDouble()),
                _ => null,
            });

        public bool Bool(string name, bool defaultValue) =>
            Read(name, defaultValue, "true 或 false", v => v.ValueKind is JsonValueKind.True or JsonValueKind.False ? new Box<bool>(v.GetBoolean()) : null);

        public string String(string name, string defaultValue) =>
            Read(name, defaultValue, "字串", v => v.ValueKind == JsonValueKind.String ? new Box<string>(v.GetString() ?? string.Empty) : null);

        public IReadOnlyList<string> StringArray(string name, IReadOnlyList<string> defaultValue) =>
            Read(name, defaultValue, "字串陣列", v =>
                v.ValueKind == JsonValueKind.Array && v.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String)
                    ? new Box<IReadOnlyList<string>>([.. v.EnumerateArray().Select(e => e.GetString() ?? string.Empty)])
                    : null);

        public TEnum Choice<TEnum>(string name, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            string allowed = string.Join(" 或 ", Enum.GetNames<TEnum>().Select(n => $"\"{n.ToLowerInvariant()}\""));
            return Read(name, defaultValue, allowed, v =>
            {
                // 只接受完整名稱（不分大小寫）；不接受數字或「a,b」組合。
                string? text = v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                string? match = Enum.GetNames<TEnum>().FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
                return match is null ? null : new Box<TEnum>(Enum.Parse<TEnum>(match));
            });
        }

        public void ReportUnknown()
        {
            if (_element is not JsonElement element)
            {
                return;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!_known.Contains(property.Name))
                {
                    _context.Warnings.Add($"未知欄位 '{Join(property.Name)}'，已忽略（請檢查拼字）。");
                }
            }
        }

        private T Read<T>(string name, T defaultValue, string expected, Func<JsonElement, Box<T>?> convert)
        {
            _known.Add(name);
            if (!TryGet(name, out JsonElement value))
            {
                if (_element is not null)
                {
                    _context.Defaulted.Add(Join(name));
                }

                return defaultValue;
            }

            if (convert(value) is not Box<T> converted)
            {
                _context.Errors.Add($"{Join(name)}：必須是{expected}（目前是 {Describe(value)}）。");
                return defaultValue;
            }

            return converted.Value;
        }

        private bool TryGet(string name, out JsonElement value)
        {
            value = default;
            if (_element is not JsonElement element)
            {
                return false;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }

        private string Join(string name) => _path.Length == 0 ? name : $"{_path}.{name}";

        private static string Describe(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => $"字串 \"{value.GetString()}\"",
            JsonValueKind.Number => $"數字 {value.GetRawText()}",
            JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            JsonValueKind.Null => "null",
            JsonValueKind.Array => "陣列",
            JsonValueKind.Object => "物件",
            _ => value.ValueKind.ToString(),
        };
    }

    /// <summary>包裝可為 null 的值，區分「轉換失敗」與「值為 null」。</summary>
    private sealed record Box<T>(T Value);
}
