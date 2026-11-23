using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;

namespace PerformanceMonitor.Presentation.Trends;

/// <summary>趨勢圖的分組（每組一張圖）。</summary>
public enum TrendGroup
{
    /// <summary>CPU 與記憶體使用率（%）。</summary>
    CpuMemory,

    /// <summary>網路下載／上傳合計（Mbps）。</summary>
    Network,

    /// <summary>各邏輯磁碟忙碌率（%）。</summary>
    DiskBusy,

    /// <summary>各 GPU 使用率（%）。</summary>
    GpuUsage,

    /// <summary>各 GPU 溫度（°C）。</summary>
    GpuTemperature,

    /// <summary>各邏輯磁碟讀取速度（MB/s）。</summary>
    DiskRead,

    /// <summary>各邏輯磁碟寫入速度（MB/s）。</summary>
    DiskWrite,

    /// <summary>各網路介面卡下載（Mbps）。</summary>
    AdapterDownload,

    /// <summary>各網路介面卡上傳（Mbps）。</summary>
    AdapterUpload,

    /// <summary>GPU 與 ACPI 熱區溫度（°C）。</summary>
    Temperature,
}

/// <summary>一次取樣的趨勢數值；數值為 null 表示該次無法取得（畫線時斷開）。</summary>
/// <param name="Time">取樣時間（UTC）。</param>
/// <param name="Values">分組 → 線名稱 → 數值。</param>
public sealed record TrendSample(DateTimeOffset Time, IReadOnlyDictionary<TrendGroup, IReadOnlyDictionary<string, double?>> Values);

/// <summary>
/// 最近 <see cref="Window"/>（5 分鐘，Phase 3b 確認）的趨勢資料，只放在記憶體。
/// 每次取樣（不是每次畫面更新）記錄一筆，畫面只在更新時讀取。停止再開始時保留舊資料，
/// 中間沒有資料的時段由圖表斷開（見 <see cref="TrendChartBuilder"/>）。執行緒安全。
/// </summary>
public sealed class TrendHistory : IDisposable
{
    /// <summary>保留的時間範圍。</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly Lock _gate = new();
    private readonly Queue<TrendSample> _samples = new();
    private readonly ISnapshotProvider? _provider;

    /// <summary>建立並訂閱每次取樣。</summary>
    public TrendHistory(ISnapshotProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        provider.SnapshotPublished += OnSnapshot;
    }

    /// <summary>建立不訂閱的實例（測試用，以 <see cref="Add"/> 加入）。</summary>
    public TrendHistory()
    {
    }

    /// <summary>目前保留的取樣（舊到新）。</summary>
    public IReadOnlyList<TrendSample> Samples
    {
        get
        {
            lock (_gate)
            {
                return [.. _samples];
            }
        }
    }

    /// <summary>加入一次取樣並捨棄超出範圍的舊資料。</summary>
    public void Add(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        TrendSample sample = Extract(snapshot);
        lock (_gate)
        {
            _samples.Enqueue(sample);
            while (_samples.Count > 0 && sample.Time - _samples.Peek().Time > Window)
            {
                _samples.Dequeue();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_provider is not null)
        {
            _provider.SnapshotPublished -= OnSnapshot;
        }
    }

    /// <summary>由快照取出各圖的數值（純函式，供測試）。模組或裝置不是 OK 時數值為 null。</summary>
    public static TrendSample Extract(MonitorSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var groups = new Dictionary<TrendGroup, IReadOnlyDictionary<string, double?>>();

        var cpu = snapshot.Find<CpuResult>();
        var memory = snapshot.Find<MemoryResult>();
        groups[TrendGroup.CpuMemory] = new Dictionary<string, double?>
        {
            ["CPU"] = Ok(cpu, cpu?.UsagePercent),
            ["記憶體"] = Ok(memory, memory?.UsagePercent),
        };

        var network = snapshot.Find<NetworkResult>();
        bool networkOk = network?.Status == MonitorStatus.OK;
        groups[TrendGroup.Network] = new Dictionary<string, double?>
        {
            ["下載"] = networkOk ? network!.TotalDownloadMbps : null,
            ["上傳"] = networkOk ? network!.TotalUploadMbps : null,
        };

        var io = snapshot.Find<DiskIoResult>();
        DiskIoItem[] logical = [.. (io?.Disks ?? []).Where(d => d.Scope == DiskIoScope.Logical)];
        groups[TrendGroup.DiskBusy] = logical.Select(d => (d.Disk, d.Status == MonitorStatus.OK ? d.BusyPercent : null)).ToSeries();
        groups[TrendGroup.DiskRead] = logical.Select(d => (d.Disk, d.Status == MonitorStatus.OK ? d.ReadMBps : null)).ToSeries();
        groups[TrendGroup.DiskWrite] = logical.Select(d => (d.Disk, d.Status == MonitorStatus.OK ? d.WriteMBps : null)).ToSeries();

        IReadOnlyList<NetworkAdapterItem> adapters = network?.Adapters ?? [];
        groups[TrendGroup.AdapterDownload] = adapters.Select(a => (a.AdapterName, a.Status == MonitorStatus.OK ? a.DownloadMbps : null)).ToSeries();
        groups[TrendGroup.AdapterUpload] = adapters.Select(a => (a.AdapterName, a.Status == MonitorStatus.OK ? a.UploadMbps : null)).ToSeries();

        var gpu = snapshot.Find<GpuResult>();
        IReadOnlyList<GpuItem> gpus = gpu?.Gpus ?? [];
        groups[TrendGroup.GpuUsage] = gpus.Select(g => (g.DeviceId, g.Status == MonitorStatus.OK ? g.UsagePercent : null)).ToSeries();
        groups[TrendGroup.GpuTemperature] = gpus.Select(g => (g.DeviceId, g.Status == MonitorStatus.OK ? g.TemperatureC : null)).ToSeries();

        var zones = snapshot.Find<TemperatureResult>();
        groups[TrendGroup.Temperature] = gpus
            .Select(g => (g.DeviceId, g.Status == MonitorStatus.OK ? g.TemperatureC : null))
            .Concat((zones?.Sensors ?? []).Select(z => (z.SensorName, z.Status == MonitorStatus.OK ? z.TemperatureC : null)))
            .ToSeries();

        return new TrendSample(snapshot.TimestampUtc, groups);
    }

    private static double? Ok(ModuleResult? result, double? value) => result?.Status == MonitorStatus.OK ? value : null;

    private void OnSnapshot(object? sender, MonitorSnapshot snapshot) => Add(snapshot);
}

/// <summary>趨勢數值的輔助方法。</summary>
internal static class TrendSeriesExtensions
{
    /// <summary>轉為線名稱 → 數值；名稱重複時取第一個（例如無法解析 index 的 GPU）。</summary>
    public static IReadOnlyDictionary<string, double?> ToSeries(this IEnumerable<(string Name, double? Value)> items)
    {
        var result = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach ((string name, double? value) in items)
        {
            result.TryAdd(name, value);
        }

        return result;
    }
}
