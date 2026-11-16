using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.Text;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>
/// 設定頁（Phase 3c 確認）：列出 settings.json 全部欄位並分區；按「儲存並重新啟動」時轉換、以
/// <see cref="SettingsValidator"/> 驗證、檢查資料目錄，全部通過才寫入（舊檔另存 .bak），再重新啟動程式套用。
/// </summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly AppSettings _current;
    private readonly ISettingsService _service;
    private readonly IApplicationRestart _restart;
    private readonly List<FieldDefinition> _definitions;
    private IReadOnlyList<string> _errors = [];
    private string? _message;

    /// <summary>建立。</summary>
    /// <param name="current">目前使用中的設定（設定檔有錯誤時為內建預設值）。</param>
    /// <param name="settingsFileValid">啟動時設定檔是否正確。</param>
    /// <param name="service">設定檔存取。</param>
    /// <param name="restart">重新啟動程式。</param>
    public SettingsViewModel(AppSettings current, bool settingsFileValid, ISettingsService service, IApplicationRestart restart)
        : base(UiText.SettingsPage)
    {
        ArgumentNullException.ThrowIfNull(service);
        _current = current;
        _service = service;
        _restart = restart;
        _definitions = Definitions();
        Sections =
        [
            .. _definitions.GroupBy(d => d.Section).Select(g => new SettingSection(g.Key, [.. g.Select(d => d.Field)])),
        ];
        Notice = settingsFileValid
            ? $"設定檔：{service.SettingsPath}。儲存時完整改寫，原檔另存為 settings.json.bak；儲存後程式會停止監控並重新啟動以套用。"
            : $"設定檔 {service.SettingsPath} 有錯誤，下方為內建預設值。儲存會取代原檔（原檔另存為 settings.json.bak），並重新啟動程式。";
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RevertCommand = new RelayCommand(() => Load(_current, "已復原為目前使用中的設定。"));
        DefaultsCommand = new RelayCommand(() => Load(new AppSettings(), "已填入內建預設值（尚未儲存）。"));
        Load(current, null);
    }

    /// <summary>各區塊。</summary>
    public IReadOnlyList<SettingSection> Sections { get; }

    /// <summary>設定檔位置與儲存方式說明。</summary>
    public string Notice { get; }

    /// <summary>上次儲存失敗的原因（每筆一行）。</summary>
    public IReadOnlyList<string> Errors
    {
        get => _errors;
        private set
        {
            if (SetProperty(ref _errors, value))
            {
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }

    /// <summary>是否有錯誤。</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>操作結果訊息。</summary>
    public string? Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>儲存並重新啟動。</summary>
    public IAsyncRelayCommand SaveCommand { get; }

    /// <summary>復原為目前使用中的設定。</summary>
    public IRelayCommand RevertCommand { get; }

    /// <summary>填入內建預設值（不儲存）。</summary>
    public IRelayCommand DefaultsCommand { get; }

    /// <summary>依名稱取得欄位（測試與畫面使用）。</summary>
    public SettingField Field(string key) => _definitions.Single(d => d.Field.Key == key).Field;

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        // 設定頁不隨取樣更新，避免覆蓋使用者正在輸入的內容。
    }

    /// <summary>由畫面上的值建立設定；轉換失敗的欄位加入 <paramref name="errors"/>。</summary>
    public AppSettings Build(List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        AppSettings settings = _current;
        foreach (FieldDefinition definition in _definitions)
        {
            settings = definition.Apply(settings, definition.Field, errors);
        }

        return settings;
    }

    private void Load(AppSettings settings, string? message)
    {
        foreach (FieldDefinition definition in _definitions)
        {
            definition.Load(settings, definition.Field);
        }

        Errors = [];
        Message = message;
    }

    private async Task SaveAsync()
    {
        var errors = new List<string>();
        AppSettings settings = Build(errors);
        if (errors.Count == 0)
        {
            errors.AddRange(SettingsValidator.Validate(settings));
        }

        if (errors.Count == 0)
        {
            errors.AddRange(_service.CheckDirectories(settings).Select(e => $"資料目錄無法寫入：{e}"));
        }

        if (errors.Count == 0)
        {
            try
            {
                _service.Save(settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"寫入設定檔失敗：{ex.Message}");
            }
        }

        Errors = errors;
        if (errors.Count > 0)
        {
            Message = "未儲存：請修正下列問題。";
            return;
        }

        Message = "已儲存，正在停止監控並重新啟動…";
        await _restart.RestartAsync().ConfigureAwait(true);
    }

    private static List<FieldDefinition> Definitions()
    {
        const string Monitor = "監控週期";
        const string Modules = "監控項目";
        const string Network = "網路介面卡篩選";
        const string Process = "程序";
        const string Thresholds = "告警門檻";
        const string Alert = "告警時間";
        const string Other = "資料、紀錄與逾時";
        return
        [
            Int(Monitor, "monitor.refreshSeconds", "畫面更新（秒）", "大於 0 的整數", s => s.Monitor.RefreshSeconds, (s, v) => s with { Monitor = s.Monitor with { RefreshSeconds = v } }),
            Int(Monitor, "monitor.sampleSeconds", "取樣週期（秒）", "大於 0 的整數", s => s.Monitor.SampleSeconds, (s, v) => s with { Monitor = s.Monitor with { SampleSeconds = v } }),
            Int(Monitor, "monitor.logSeconds", "CSV 紀錄週期（秒）", "大於 0 的整數", s => s.Monitor.LogSeconds, (s, v) => s with { Monitor = s.Monitor with { LogSeconds = v } }),

            Bool(Modules, "cpu.enabled", "CPU", s => s.Cpu.Enabled, (s, v) => s with { Cpu = s.Cpu with { Enabled = v } }),
            Bool(Modules, "memory.enabled", "記憶體", s => s.Memory.Enabled, (s, v) => s with { Memory = s.Memory with { Enabled = v } }),
            Bool(Modules, "disk.enabled", "磁碟容量", s => s.Disk.Enabled, (s, v) => s with { Disk = s.Disk with { Enabled = v } }),
            Bool(Modules, "diskIo.enabled", "磁碟 I/O", s => s.DiskIo.Enabled, (s, v) => s with { DiskIo = s.DiskIo with { Enabled = v } }),
            Bool(Modules, "gpu.enabled", "NVIDIA GPU", s => s.Gpu.Enabled, (s, v) => s with { Gpu = s.Gpu with { Enabled = v } }),
            Bool(Modules, "network.enabled", "網路", s => s.Network.Enabled, (s, v) => s with { Network = s.Network with { Enabled = v } }),
            Bool(Modules, "process.enabled", "程序", s => s.Process.Enabled, (s, v) => s with { Process = s.Process with { Enabled = v } }),
            Bool(Modules, "temperature.enabled", "ACPI 熱區（不是 CPU 溫度）", s => s.Temperature.Enabled, (s, v) => s with { Temperature = s.Temperature with { Enabled = v } }),

            Bool(Network, "network.physicalOnlyByDefault", "未指定納入名稱時只納入實體介面卡", s => s.Network.PhysicalOnlyByDefault, (s, v) => s with { Network = s.Network with { PhysicalOnlyByDefault = v } }),
            Names(Network, "network.includeNames", "納入的名稱", "名稱或描述，萬用字元 * ?；以逗號或換行分隔；有內容時取代預設判斷", s => s.Network.IncludeNames, (s, v) => s with { Network = s.Network with { IncludeNames = v } }),
            Names(Network, "network.excludeNames", "排除的名稱", "同上；永遠優先", s => s.Network.ExcludeNames, (s, v) => s with { Network = s.Network with { ExcludeNames = v } }),
            Bool(Network, "network.aggregateIncludedAdaptersOnly", "合計只加總納入的介面卡", s => s.Network.AggregateIncludedAdaptersOnly, (s, v) => s with { Network = s.Network with { AggregateIncludedAdaptersOnly = v } }, "保留欄位，目前不影響行為"),

            Int(Process, "process.topCount", "列出前幾名", "1～20", s => s.Process.TopCount, (s, v) => s with { Process = s.Process with { TopCount = v } }),
            Bool(Process, "process.loggingEnabled", "寫入 ProcessMetrics CSV", s => s.Process.LoggingEnabled, (s, v) => s with { Process = s.Process with { LoggingEnabled = v } }),
            Int(Process, "process.logIntervalSeconds", "程序紀錄週期（秒）", "大於 0 的整數", s => s.Process.LogIntervalSeconds, (s, v) => s with { Process = s.Process with { LogIntervalSeconds = v } }),

            Num(Thresholds, "thresholds.cpuPercent", "CPU 告警（%）", "0～100", s => s.Thresholds.CpuPercent, (s, v) => s with { Thresholds = s.Thresholds with { CpuPercent = v } }),
            Num(Thresholds, "thresholds.cpuRecoveryPercent", "CPU 恢復（%）", "0～100，小於告警值", s => s.Thresholds.CpuRecoveryPercent, (s, v) => s with { Thresholds = s.Thresholds with { CpuRecoveryPercent = v } }),
            Num(Thresholds, "thresholds.ramPercent", "記憶體告警（%）", "0～100", s => s.Thresholds.RamPercent, (s, v) => s with { Thresholds = s.Thresholds with { RamPercent = v } }),
            Num(Thresholds, "thresholds.ramRecoveryPercent", "記憶體恢復（%）", "0～100，小於告警值", s => s.Thresholds.RamRecoveryPercent, (s, v) => s with { Thresholds = s.Thresholds with { RamRecoveryPercent = v } }),
            Num(Thresholds, "thresholds.diskFreePercent", "磁碟可用低於（%）", "0～100", s => s.Thresholds.DiskFreePercent, (s, v) => s with { Thresholds = s.Thresholds with { DiskFreePercent = v } }),
            Num(Thresholds, "thresholds.diskRecoveryFreePercent", "磁碟可用恢復（%）", "0～100，大於告警值", s => s.Thresholds.DiskRecoveryFreePercent, (s, v) => s with { Thresholds = s.Thresholds with { DiskRecoveryFreePercent = v } }),
            OptNum(Thresholds, "thresholds.diskFreeGB", "磁碟可用低於（GB）", "可空白；與恢復值成對設定", s => s.Thresholds.DiskFreeGB, (s, v) => s with { Thresholds = s.Thresholds with { DiskFreeGB = v } }),
            OptNum(Thresholds, "thresholds.diskRecoveryFreeGB", "磁碟可用恢復（GB）", "可空白；大於告警值", s => s.Thresholds.DiskRecoveryFreeGB, (s, v) => s with { Thresholds = s.Thresholds with { DiskRecoveryFreeGB = v } }),
            Choice(Thresholds, "thresholds.diskConditionMode", "磁碟條件", "any：百分比或 GB 任一符合；all：兩者都符合", ["any", "all"],
                s => s.Thresholds.DiskConditionMode == DiskConditionMode.All ? "all" : "any",
                (s, v) => s with { Thresholds = s.Thresholds with { DiskConditionMode = v == "all" ? DiskConditionMode.All : DiskConditionMode.Any } }),

            Bool(Alert, "alert.enabled", "啟用告警", s => s.Alert.Enabled, (s, v) => s with { Alert = s.Alert with { Enabled = v } }),
            Int(Alert, "alert.durationSeconds", "持續多久才告警（秒）", "0～86400", s => s.Alert.DurationSeconds, (s, v) => s with { Alert = s.Alert with { DurationSeconds = v } }),
            Int(Alert, "alert.cooldownSeconds", "冷卻時間（秒）", "0～86400", s => s.Alert.CooldownSeconds, (s, v) => s with { Alert = s.Alert with { CooldownSeconds = v } }),
            Int(Alert, "alert.recoveryDurationSeconds", "持續多久才恢復（秒）", "0～86400", s => s.Alert.RecoveryDurationSeconds, (s, v) => s with { Alert = s.Alert with { RecoveryDurationSeconds = v } }),
            Bool(Alert, "alert.repeatNotificationAfterCooldown", "冷卻後仍異常時再次通知", s => s.Alert.RepeatNotificationAfterCooldown, (s, v) => s with { Alert = s.Alert with { RepeatNotificationAfterCooldown = v } }),

            Bool(Other, "logging.enabled", "寫入 CSV", s => s.Logging.Enabled, (s, v) => s with { Logging = s.Logging with { Enabled = v } }, "關閉時只停止 CSV，Log 照常"),
            Str(Other, "logging.dataDirectory", "CSV 目錄", "相對路徑以 %LOCALAPPDATA%\\WindowsPerformanceMonitor 為基準", s => s.Logging.DataDirectory, (s, v) => s with { Logging = s.Logging with { DataDirectory = v } }),
            Str(Other, "logging.logDirectory", "Log 目錄", "同上", s => s.Logging.LogDirectory, (s, v) => s with { Logging = s.Logging with { LogDirectory = v } }),
            Int(Other, "logging.lockedFileRetryCount", "檔案被鎖定時重試次數", "0～5", s => s.Logging.LockedFileRetryCount, (s, v) => s with { Logging = s.Logging with { LockedFileRetryCount = v } }),
            Int(Other, "timeouts.gpuSeconds", "nvidia-smi 逾時（秒）", "1～30", s => s.Timeouts.GpuSeconds, (s, v) => s with { Timeouts = s.Timeouts with { GpuSeconds = v } }),
        ];
    }

    private static FieldDefinition Bool(string section, string key, string label, Func<AppSettings, bool> get, Func<AppSettings, bool, AppSettings> set, string hint = "") =>
        new(section, new SettingField(key, label, hint, SettingKind.Boolean), (s, f) => f.Checked = get(s), (s, f, _) => set(s, f.Checked));

    private static FieldDefinition Int(string section, string key, string label, string hint, Func<AppSettings, int> get, Func<AppSettings, int, AppSettings> set) =>
        new(section, new SettingField(key, label, hint, SettingKind.Text), (s, f) => f.Text = get(s).ToString(CultureInfo.InvariantCulture), (s, f, errors) =>
        {
            if (int.TryParse(f.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
            {
                return set(s, value);
            }

            errors.Add($"{key}（{label}）必須是整數：「{f.Text}」");
            return s;
        });

    private static FieldDefinition Num(string section, string key, string label, string hint, Func<AppSettings, double> get, Func<AppSettings, double, AppSettings> set) =>
        new(section, new SettingField(key, label, hint, SettingKind.Text), (s, f) => f.Text = get(s).ToString(CultureInfo.InvariantCulture), (s, f, errors) =>
        {
            if (TryNumber(f.Text, out double value))
            {
                return set(s, value);
            }

            errors.Add($"{key}（{label}）必須是數字：「{f.Text}」");
            return s;
        });

    private static FieldDefinition OptNum(string section, string key, string label, string hint, Func<AppSettings, double?> get, Func<AppSettings, double?, AppSettings> set) =>
        new(section, new SettingField(key, label, hint, SettingKind.OptionalText), (s, f) => f.Text = get(s)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, (s, f, errors) =>
        {
            if (string.IsNullOrWhiteSpace(f.Text))
            {
                return set(s, null);
            }

            if (TryNumber(f.Text, out double value))
            {
                return set(s, value);
            }

            errors.Add($"{key}（{label}）必須是數字或空白：「{f.Text}」");
            return s;
        });

    private static FieldDefinition Str(string section, string key, string label, string hint, Func<AppSettings, string> get, Func<AppSettings, string, AppSettings> set) =>
        new(section, new SettingField(key, label, hint, SettingKind.Text), (s, f) => f.Text = get(s), (s, f, _) => set(s, f.Text.Trim()));

    private static FieldDefinition Names(string section, string key, string label, string hint, Func<AppSettings, IReadOnlyList<string>> get, Func<AppSettings, IReadOnlyList<string>, AppSettings> set) =>
        new(section, new SettingField(key, label, hint, SettingKind.List), (s, f) => f.Text = string.Join(", ", get(s)), (s, f, _) =>
            set(s, [.. f.Text.Split([',', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]));

    private static FieldDefinition Choice(string section, string key, string label, string hint, IReadOnlyList<string> options, Func<AppSettings, string> get, Func<AppSettings, string, AppSettings> set) =>
        new(section, new SettingField(key, label, hint, SettingKind.Choice) { Options = options }, (s, f) => f.Text = get(s), (s, f, _) => set(s, f.Text));

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>欄位定義：所屬區塊、欄位、由設定載入、套用到設定。</summary>
    private sealed record FieldDefinition(
        string Section,
        SettingField Field,
        Action<AppSettings, SettingField> Load,
        Func<AppSettings, SettingField, List<string>, AppSettings> Apply);
}
