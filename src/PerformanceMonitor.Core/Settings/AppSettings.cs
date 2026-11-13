namespace PerformanceMonitor.Core.Settings;

/// <summary>
/// 全部設定（對應 settings.json）。預設值即內建設定；欄位沿用舊版 v1.5 §9 並精簡，
/// 見 docs/Specification_v2.0.md §7。
/// </summary>
public sealed record AppSettings
{
    /// <summary>週期設定。</summary>
    public MonitorSettings Monitor { get; init; } = new();

    /// <summary>CPU。</summary>
    public ModuleSettings Cpu { get; init; } = new();

    /// <summary>記憶體。</summary>
    public ModuleSettings Memory { get; init; } = new();

    /// <summary>磁碟容量。</summary>
    public ModuleSettings Disk { get; init; } = new();

    /// <summary>磁碟 I/O。</summary>
    public ModuleSettings DiskIo { get; init; } = new();

    /// <summary>NVIDIA GPU。</summary>
    public ModuleSettings Gpu { get; init; } = new();

    /// <summary>網路。</summary>
    public NetworkSettings Network { get; init; } = new();

    /// <summary>程序。</summary>
    public ProcessSettings Process { get; init; } = new();

    /// <summary>ACPI 熱區溫度。</summary>
    public ModuleSettings Temperature { get; init; } = new();

    /// <summary>告警門檻。</summary>
    public ThresholdSettings Thresholds { get; init; } = new();

    /// <summary>告警時間規則。</summary>
    public AlertSettings Alert { get; init; } = new();

    /// <summary>逾時。</summary>
    public TimeoutSettings Timeouts { get; init; } = new();

    /// <summary>CSV 與 Log。</summary>
    public LoggingSettings Logging { get; init; } = new();
}

/// <summary>取樣、畫面與 CSV 週期（秒，整數）。</summary>
public sealed record MonitorSettings
{
    /// <summary>畫面更新週期。</summary>
    public int RefreshSeconds { get; init; } = 2;

    /// <summary>取樣週期。</summary>
    public int SampleSeconds { get; init; } = 2;

    /// <summary>CSV 紀錄週期。</summary>
    public int LogSeconds { get; init; } = 10;
}

/// <summary>只有啟用開關的模組設定。</summary>
public sealed record ModuleSettings
{
    /// <summary>是否啟用。</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>網路介面卡篩選。</summary>
public sealed record NetworkSettings
{
    /// <summary>是否啟用。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>未指定 <see cref="IncludeNames"/> 時，是否只納入實體介面卡。</summary>
    public bool PhysicalOnlyByDefault { get; init; } = true;

    /// <summary>指定納入的介面卡名稱或描述（萬用字元 * ?）；有內容時取代預設判斷。</summary>
    public IReadOnlyList<string> IncludeNames { get; init; } = [];

    /// <summary>排除的介面卡名稱或描述；永遠優先。</summary>
    public IReadOnlyList<string> ExcludeNames { get; init; } = [];

    /// <summary>合計速度只加總納入的介面卡。</summary>
    public bool AggregateIncludedAdaptersOnly { get; init; } = true;
}

/// <summary>程序 Top N。</summary>
public sealed record ProcessSettings
{
    /// <summary>是否啟用。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>顯示與紀錄的程序數（1～20）。</summary>
    public int TopCount { get; init; } = 5;

    /// <summary>是否寫入程序 CSV。</summary>
    public bool LoggingEnabled { get; init; } = true;

    /// <summary>程序 CSV 紀錄間隔（秒）；只在 CSV 紀錄時點檢查。</summary>
    public int LogIntervalSeconds { get; init; } = 10;
}

/// <summary>磁碟告警條件組合方式。</summary>
public enum DiskConditionMode
{
    /// <summary>任一已設定條件達門檻即觸發。</summary>
    Any,

    /// <summary>所有已設定條件都達門檻才觸發。</summary>
    All,
}

/// <summary>告警門檻（百分比 0～100）。</summary>
public sealed record ThresholdSettings
{
    /// <summary>CPU 觸發值。</summary>
    public double CpuPercent { get; init; } = 90;

    /// <summary>CPU 恢復值。</summary>
    public double CpuRecoveryPercent { get; init; } = 85;

    /// <summary>RAM 觸發值。</summary>
    public double RamPercent { get; init; } = 90;

    /// <summary>RAM 恢復值。</summary>
    public double RamRecoveryPercent { get; init; } = 85;

    /// <summary>磁碟可用空間百分比觸發值（小於或等於）。</summary>
    public double DiskFreePercent { get; init; } = 10;

    /// <summary>磁碟可用空間百分比恢復值（大於或等於）。</summary>
    public double DiskRecoveryFreePercent { get; init; } = 12;

    /// <summary>磁碟可用 GB 觸發值；null 表示不使用。</summary>
    public double? DiskFreeGB { get; init; }

    /// <summary>磁碟可用 GB 恢復值；須與 <see cref="DiskFreeGB"/> 成對設定。</summary>
    public double? DiskRecoveryFreeGB { get; init; }

    /// <summary>百分比與 GB 條件的組合方式。</summary>
    public DiskConditionMode DiskConditionMode { get; init; } = DiskConditionMode.Any;
}

/// <summary>告警時間規則（秒，0～86400）。</summary>
public sealed record AlertSettings
{
    /// <summary>是否啟用告警。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>持續達門檻多久才告警。</summary>
    public int DurationSeconds { get; init; } = 10;

    /// <summary>通知冷卻時間。</summary>
    public int CooldownSeconds { get; init; } = 60;

    /// <summary>持續達恢復值多久才宣告恢復。</summary>
    public int RecoveryDurationSeconds { get; init; } = 10;

    /// <summary>冷卻結束且仍異常時是否再次通知。</summary>
    public bool RepeatNotificationAfterCooldown { get; init; } = true;
}

/// <summary>外部命令逾時（秒，1～30）。</summary>
public sealed record TimeoutSettings
{
    /// <summary>nvidia-smi 逾時。</summary>
    public int GpuSeconds { get; init; } = 3;
}

/// <summary>CSV 與 Log。相對路徑以 %LOCALAPPDATA%\WindowsPerformanceMonitor 為基準。</summary>
public sealed record LoggingSettings
{
    /// <summary>是否寫入 CSV（Application／Error Log 不受影響）。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>CSV 目錄。</summary>
    public string DataDirectory { get; init; } = "Data";

    /// <summary>Log 目錄。</summary>
    public string LogDirectory { get; init; } = "Logs";

    /// <summary>檔案被鎖定時的重試次數（0～5）。</summary>
    public int LockedFileRetryCount { get; init; } = 1;
}
