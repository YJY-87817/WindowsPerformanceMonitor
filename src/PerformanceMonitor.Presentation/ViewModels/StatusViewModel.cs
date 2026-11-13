using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Presentation.Text;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>模組或裝置的狀態列。</summary>
/// <param name="Module">模組名稱。</param>
/// <param name="Device">裝置（磁碟、GPU、介面卡、熱區）；模組層級為 null。</param>
/// <param name="StatusText">狀態文字。</param>
/// <param name="Kind">狀態分類（顏色）。</param>
/// <param name="ErrorCode">錯誤分類；沒有時為 null。</param>
/// <param name="Message">原因；沒有時為 null。</param>
public sealed record StatusRow(string Module, string? Device, string StatusText, StatusKind Kind, string? ErrorCode, string? Message);

/// <summary>狀態與錯誤：各模組狀態、不正常的裝置、最近告警、CSV 寫入統計、啟動時的問題。</summary>
public sealed class StatusViewModel : PageViewModel
{
    private readonly IStorageStatus _storage;
    private IReadOnlyList<StatusRow> _modules = [];
    private IReadOnlyList<StatusRow> _deviceIssues = [];
    private IReadOnlyList<string> _recentAlerts = [];
    private string _storageText = string.Empty;
    private StatusKind _storageKind = StatusKind.Ok;

    /// <summary>建立。</summary>
    /// <param name="storage">CSV 寫入統計。</param>
    /// <param name="startup">啟動時的設定與目錄檢查結果。</param>
    public StatusViewModel(IStorageStatus storage, StartupStatus startup)
        : base(UiText.StatusPage)
    {
        ArgumentNullException.ThrowIfNull(startup);
        _storage = storage;
        StartupLines = [.. startup.Errors.Select(e => $"錯誤：{e}"), .. startup.Warnings.Select(w => $"警告：{w}")];
    }

    /// <summary>各模組狀態。</summary>
    public IReadOnlyList<StatusRow> Modules
    {
        get => _modules;
        private set => SetProperty(ref _modules, value);
    }

    /// <summary>不是 OK 的裝置。</summary>
    public IReadOnlyList<StatusRow> DeviceIssues
    {
        get => _deviceIssues;
        private set => SetProperty(ref _deviceIssues, value);
    }

    /// <summary>最近告警事件（新到舊，最多 5 筆）。</summary>
    public IReadOnlyList<string> RecentAlerts
    {
        get => _recentAlerts;
        private set => SetProperty(ref _recentAlerts, value);
    }

    /// <summary>CSV 寫入統計。</summary>
    public string StorageText
    {
        get => _storageText;
        private set => SetProperty(ref _storageText, value);
    }

    /// <summary>CSV 寫入狀態分類：有遺失列或最近錯誤時為 Error。</summary>
    public StatusKind StorageKind
    {
        get => _storageKind;
        private set => SetProperty(ref _storageKind, value);
    }

    /// <summary>啟動時設定與資料目錄的錯誤與警告。</summary>
    public IReadOnlyList<string> StartupLines { get; }

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        MonitorSnapshot? snapshot = data.Snapshot;
        if (snapshot is not null)
        {
            Modules = [.. snapshot.Results.Select(r => Row(r.Module, null, r.Status, r.ErrorCode, r.ErrorMessage))];
            DeviceIssues =
            [
                .. snapshot.Results.OfType<IDeviceResult>()
                    .SelectMany(r => r.Devices.Select(d => (Module: ((ModuleResult)r).Module, Device: d)))
                    .Where(x => x.Device.Status != MonitorStatus.OK)
                    .Select(x => Row(x.Module, x.Device.DeviceId, x.Device.Status, x.Device.ErrorCode, x.Device.ErrorMessage)),
            ];
        }

        RecentAlerts = !data.Alerts.Enabled
            ? ["告警已停用"]
            : data.Alerts.Recent.Count == 0
                ? ["沒有告警事件"]
                : [.. data.Alerts.Recent.Select(e => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{e.Time:MM-dd HH:mm:ss}  {e.Target} {UiText.AlertEvent(e.Type)}：{e.ValueText}（{e.Threshold}）"))];

        StorageText = string.Create(
            CultureInfo.InvariantCulture,
            $"CSV 已寫入 {_storage.WrittenRows} 列，遺失 {_storage.LostRows} 列{(_storage.LastError is null ? string.Empty : $"；最近錯誤：{_storage.LastError}")}");
        StorageKind = _storage.LostRows > 0 || _storage.LastError is not null ? StatusKind.Error : StatusKind.Ok;
    }

    private static StatusRow Row(string module, string? device, MonitorStatus status, ErrorCode? code, string? message) =>
        new(UiText.ModuleTitle(module), device, UiText.Status(status), UiText.Kind(status), code?.ToString(), message);
}
