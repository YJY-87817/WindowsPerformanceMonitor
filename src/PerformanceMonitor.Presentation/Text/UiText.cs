using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Presentation.Text;

/// <summary>狀態的顯示分類（畫面依此決定顏色）。</summary>
public enum StatusKind
{
    /// <summary>正常。</summary>
    Ok,

    /// <summary>暖機中或停止中等暫時狀態。</summary>
    Pending,

    /// <summary>無法使用或已停用（不是錯誤）。</summary>
    Inactive,

    /// <summary>錯誤。</summary>
    Error,
}

/// <summary>
/// 畫面文字（繁體中文，Phase 3 確認）集中在此，日後要加其他語言只改這裡。
/// 狀態代碼保留英文（與 CSV、Log 一致）並附中文說明。
/// </summary>
public static class UiText
{
    /// <summary>視窗標題。</summary>
    public const string AppTitle = "Windows 效能監控";

    /// <summary>沒有數值時顯示。</summary>
    public const string NoValue = "—";

    /// <summary>頁面：總覽。</summary>
    public const string OverviewPage = "總覽";

    /// <summary>頁面：狀態與錯誤。</summary>
    public const string StatusPage = "狀態與錯誤";

    /// <summary>模組中文名稱。</summary>
    public static string ModuleTitle(string module) => module switch
    {
        ModuleNames.Cpu => "CPU",
        ModuleNames.Memory => "記憶體",
        ModuleNames.Disk => "磁碟容量",
        ModuleNames.DiskIo => "磁碟 I/O",
        ModuleNames.Gpu => "NVIDIA GPU",
        ModuleNames.Network => "網路",
        ModuleNames.Process => "程序（RAM 前幾名）",
        ModuleNames.Temperature => "ACPI 熱區",
        _ => module,
    };

    /// <summary>狀態文字，例如「正常（OK）」。</summary>
    public static string Status(MonitorStatus status) => status switch
    {
        MonitorStatus.OK => "正常（OK）",
        MonitorStatus.WarmingUp => "暖機中（WarmingUp）",
        MonitorStatus.Unavailable => "無法使用（Unavailable）",
        MonitorStatus.Error => "錯誤（Error）",
        MonitorStatus.Disabled => "已停用（Disabled）",
        _ => status.ToString(),
    };

    /// <summary>狀態的顯示分類。</summary>
    public static StatusKind Kind(MonitorStatus status) => status switch
    {
        MonitorStatus.OK => StatusKind.Ok,
        MonitorStatus.WarmingUp => StatusKind.Pending,
        MonitorStatus.Error => StatusKind.Error,
        _ => StatusKind.Inactive,
    };

    /// <summary>監控狀態文字。</summary>
    public static string Monitoring(MonitoringState state) => state switch
    {
        MonitoringState.Running => "監控中",
        MonitoringState.Stopping => "停止中…",
        MonitoringState.Faulted => "監控異常結束",
        _ => "已停止",
    };

    /// <summary>監控狀態的顯示分類。</summary>
    public static StatusKind Kind(MonitoringState state) => state switch
    {
        MonitoringState.Running => StatusKind.Ok,
        MonitoringState.Stopping => StatusKind.Pending,
        MonitoringState.Faulted => StatusKind.Error,
        _ => StatusKind.Inactive,
    };

    /// <summary>告警事件類型文字。</summary>
    public static string AlertEvent(AlertEventType type) => type switch
    {
        AlertEventType.Warning => "告警",
        AlertEventType.Repeat => "仍在告警",
        AlertEventType.Recovered => "已恢復",
        _ => type.ToString(),
    };

    /// <summary>進行中告警的狀態文字。</summary>
    public static string AlertState(AlertState state) => state switch
    {
        Core.Models.Alerts.AlertState.Cooldown => "告警中",
        Core.Models.Alerts.AlertState.Recovering => "恢復中",
        Core.Models.Alerts.AlertState.Pending => "觀察中",
        _ => "正常",
    };
}
