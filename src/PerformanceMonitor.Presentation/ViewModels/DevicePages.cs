using System.Globalization;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Presentation.Text;
using static PerformanceMonitor.Presentation.Text.ModuleSummary;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>磁碟容量一列（文字已格式化；無法取得為「—」）。</summary>
public sealed record DiskCapacityRow(string Drive, string Label, string Total, string Used, string Free, string UsedPercent, string StatusText, StatusKind Kind, string? Message);

/// <summary>磁碟 I/O 一列。</summary>
public sealed record DiskIoRow(string Scope, string Disk, string Read, string Write, string Iops, string Latency, string Busy, string StatusText, StatusKind Kind, string? Message);

/// <summary>網路介面卡一列。</summary>
public sealed record NetworkRow(string Name, string Description, string Physical, string Download, string Upload, string StatusText, StatusKind Kind, string? Message);

/// <summary>溫度感測器一列。</summary>
public sealed record TemperatureRow(string Type, string Name, string Temperature, string StatusText, StatusKind Kind, string? Message);

/// <summary>程序一列。</summary>
public sealed record ProcessRow(string Rank, string Name, string Pid, string WorkingSet, string StartTime);

/// <summary>有模組狀態列的分頁共用：模組狀態文字與原因。</summary>
/// <param name="title">頁面名稱。</param>
public abstract class ModulePageViewModel(string title) : PageViewModel(title)
{
    private string _moduleStatus = "尚未取樣";
    private StatusKind _moduleKind = StatusKind.Inactive;

    /// <summary>模組狀態（多個模組時以「；」分隔）與原因。</summary>
    public string ModuleStatus
    {
        get => _moduleStatus;
        protected set => SetProperty(ref _moduleStatus, value);
    }

    /// <summary>最差的模組狀態分類。</summary>
    public StatusKind ModuleKind
    {
        get => _moduleKind;
        protected set => SetProperty(ref _moduleKind, value);
    }

    /// <summary>以模組結果更新狀態列。</summary>
    protected void SetStatus(params ModuleResult?[] results)
    {
        ModuleResult[] present = [.. results.OfType<ModuleResult>()];
        if (present.Length == 0)
        {
            return;
        }

        ModuleStatus = string.Join("；", present.Select(r =>
            $"{UiText.ModuleTitle(r.Module)}：{UiText.Status(r.Status)}{(r.ErrorMessage is null ? string.Empty : $"（{r.ErrorMessage}）")}"));
        ModuleKind = present.Select(r => UiText.Kind(r.Status)).Max();
    }

    /// <summary>以「讀 / 寫」格式組合兩個數值。</summary>
    protected static string Pair(double? first, double? second, string format) => $"{Number(first, format)} / {Number(second, format)}";
}

/// <summary>磁碟：容量與 I/O（邏輯與實體）。</summary>
public sealed class DiskViewModel() : ModulePageViewModel(UiText.DiskPage)
{
    private IReadOnlyList<DiskCapacityRow> _capacity = [];
    private IReadOnlyList<DiskIoRow> _io = [];

    /// <summary>各固定磁碟容量。</summary>
    public IReadOnlyList<DiskCapacityRow> Capacity
    {
        get => _capacity;
        private set => SetProperty(ref _capacity, value);
    }

    /// <summary>各磁碟 I/O（先邏輯、後實體）。</summary>
    public IReadOnlyList<DiskIoRow> Io
    {
        get => _io;
        private set => SetProperty(ref _io, value);
    }

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Snapshot is not MonitorSnapshot s)
        {
            return;
        }

        var disk = s.Find<DiskResult>();
        var io = s.Find<DiskIoResult>();
        SetStatus(s.Find(ModuleNames.Disk), s.Find(ModuleNames.DiskIo));
        Capacity = [.. (disk?.Disks ?? []).Select(d => new DiskCapacityRow(
            d.Drive, d.VolumeLabel ?? string.Empty, Gb(d.TotalGB), Gb(d.UsedGB), Gb(d.FreeGB), Percent(d.UsedPercent),
            UiText.Status(d.Status), UiText.Kind(d.Status), d.ErrorMessage))];
        Io = [.. (io?.Disks ?? []).Select(d => new DiskIoRow(
            d.Scope == DiskIoScope.Logical ? "邏輯" : "實體", d.Disk,
            $"{Number(d.ReadMBps, "0.00")} MB/s", $"{Number(d.WriteMBps, "0.00")} MB/s",
            Pair(d.ReadIops, d.WriteIops, "0"), $"{Pair(d.ReadLatencyMs, d.WriteLatencyMs, "0.00")} ms", Percent(d.BusyPercent),
            UiText.Status(d.Status), UiText.Kind(d.Status), d.ErrorMessage))];
    }

    private static string Gb(double? value) => value is null ? UiText.NoValue : $"{Number(value, "0.00")} GB";
}

/// <summary>網路：納入統計的介面卡與合計。</summary>
public sealed class NetworkViewModel() : ModulePageViewModel(UiText.NetworkPage)
{
    private IReadOnlyList<NetworkRow> _adapters = [];
    private string _totalText = UiText.NoValue;

    /// <summary>納入統計的介面卡（篩選規則見設定 network.*；啟動 Log 記錄排除的介面卡與原因）。</summary>
    public IReadOnlyList<NetworkRow> Adapters
    {
        get => _adapters;
        private set => SetProperty(ref _adapters, value);
    }

    /// <summary>合計（只加總狀態 OK 的介面卡）。</summary>
    public string TotalText
    {
        get => _totalText;
        private set => SetProperty(ref _totalText, value);
    }

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Snapshot is not MonitorSnapshot s)
        {
            return;
        }

        var network = s.Find<NetworkResult>();
        SetStatus(s.Find(ModuleNames.Network));
        IReadOnlyList<NetworkAdapterItem> adapters = network?.Adapters ?? [];
        Adapters = [.. adapters.Select(a => new NetworkRow(
            a.AdapterName, a.InterfaceDescription ?? string.Empty, a.IsPhysical switch { true => "實體", false => "虛擬", _ => UiText.NoValue },
            $"{Number(a.DownloadMbps, "0.00")} Mbps", $"{Number(a.UploadMbps, "0.00")} Mbps",
            UiText.Status(a.Status), UiText.Kind(a.Status), a.ErrorMessage))];
        TotalText = network is null || adapters.Count == 0
            ? UiText.NoValue
            : string.Create(
                CultureInfo.InvariantCulture,
                $"下載 {Number(network.TotalDownloadMbps, "0.00")} Mbps／上傳 {Number(network.TotalUploadMbps, "0.00")} Mbps（{adapters.Count(a => a.Status == MonitorStatus.OK)} / {adapters.Count} 張介面卡）");
    }
}

/// <summary>溫度：GPU 與 ACPI 熱區。</summary>
public sealed class TemperatureViewModel() : ModulePageViewModel(UiText.TemperaturePage)
{
    private IReadOnlyList<TemperatureRow> _sensors = [];

    /// <summary>各感測器（先 GPU、後 ACPI 熱區）。</summary>
    public IReadOnlyList<TemperatureRow> Sensors
    {
        get => _sensors;
        private set => SetProperty(ref _sensors, value);
    }

    /// <summary>ACPI 熱區說明。</summary>
    public string Note { get; } = UiText.ThermalZoneNote;

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Snapshot is not MonitorSnapshot s)
        {
            return;
        }

        var gpu = s.Find<GpuResult>();
        var zones = s.Find<TemperatureResult>();
        SetStatus(s.Find(ModuleNames.Gpu), s.Find(ModuleNames.Temperature));
        Sensors =
        [
            .. (gpu?.Gpus ?? []).Select(g => new TemperatureRow(
                "GPU", $"{g.DeviceId} {g.GpuName}", Celsius(g.Status, g.TemperatureC), UiText.Status(g.Status), UiText.Kind(g.Status), g.ErrorMessage)),
            .. (zones?.Sensors ?? []).Select(z => new TemperatureRow(
                "ACPI 熱區", z.SensorName, Celsius(z.Status, z.TemperatureC), UiText.Status(z.Status), UiText.Kind(z.Status), z.ErrorMessage)),
        ];
    }

    private static string Celsius(MonitorStatus status, double? value) =>
        status == MonitorStatus.OK && value is not null ? $"{Number(value, "0.0")} °C" : UiText.NoValue;
}

/// <summary>程序：RAM（Working Set）前 N 名。</summary>
public sealed class ProcessViewModel() : ModulePageViewModel(UiText.ProcessPage)
{
    private IReadOnlyList<ProcessRow> _processes = [];
    private string _countsText = string.Empty;

    /// <summary>前 N 名（依 Working Set 由大到小）。</summary>
    public IReadOnlyList<ProcessRow> Processes
    {
        get => _processes;
        private set => SetProperty(ref _processes, value);
    }

    /// <summary>略過與缺欄位的程序數。</summary>
    public string CountsText
    {
        get => _countsText;
        private set => SetProperty(ref _countsText, value);
    }

    /// <summary>說明。</summary>
    public string Note { get; } = "Working Set 是程序目前使用的實體記憶體；各程序加總不等於系統記憶體使用量。不提供 CPU 排序。";

    /// <inheritdoc />
    public override void Refresh(RefreshData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Snapshot is not MonitorSnapshot s)
        {
            return;
        }

        var process = s.Find<ProcessResult>();
        SetStatus(s.Find(ModuleNames.Process));
        Processes = [.. (process?.Processes ?? []).Select(p => new ProcessRow(
            p.Rank.ToString(CultureInfo.InvariantCulture), p.ProcessName, p.Pid.ToString(CultureInfo.InvariantCulture),
            $"{Number(p.WorkingSetMB, "0.0")} MB",
            p.StartTimeUtc is DateTimeOffset t ? t.ToOffset(s.LocalOffset).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : UiText.NoValue))];
        CountsText = process is null
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"略過 {process.SkippedProcessCount} 個程序（已結束或無法讀取）；{process.MissingFieldCount} 個程序的啟動時間無權讀取");
    }
}
