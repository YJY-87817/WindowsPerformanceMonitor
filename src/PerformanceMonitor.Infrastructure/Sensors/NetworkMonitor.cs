using System.ComponentModel;
using System.Text.RegularExpressions;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Network;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>單張介面卡的篩選結果。</summary>
/// <param name="Reading">讀數。</param>
/// <param name="IsPhysical">是否判斷為實體介面卡。</param>
/// <param name="Included">是否納入統計。</param>
/// <param name="Explicit">是否由 includeNames 明確納入。</param>
/// <param name="Reason">判斷原因（default、includeNames、excludeNames、not in includeNames、not connected、virtual）。</param>
public sealed record AdapterSelection(InterfaceReading Reading, bool IsPhysical, bool Included, bool Explicit, string Reason);

/// <summary>速度計算基準。</summary>
/// <param name="ReceivedBytes">累積接收位元組。</param>
/// <param name="SentBytes">累積傳送位元組。</param>
/// <param name="Time">單調時間。</param>
/// <param name="Name">當時的名稱（改變時重新暖機）。</param>
/// <param name="Description">當時的描述。</param>
public sealed record NetworkBaseline(ulong ReceivedBytes, ulong SentBytes, TimeSpan Time, string Name, string Description);

/// <summary>
/// 網路介面卡流量：Win32 <c>GetIfTable2</c>（盤點 M7），篩選與速度規則移植自舊規格 v1.5 §3.6（v1.4 決策）。
/// 以兩次取樣的累積位元組差與單調時間計算十進位 Mbps；第一次、計數器變小、識別資訊改變或超過連線速度 1.1 倍時為 WarmingUp。
/// 查詢失敗時為 Error 並保留基準，恢復後以實際經過時間繼續計算。
/// </summary>
public sealed class NetworkMonitor(AppSettings settings, INetworkInterfaceSource source, IClock clock) : IMonitor
{
    /// <summary>超過連線速度此倍數的速度視為計數器重設造成的不合理尖峰。</summary>
    public const double SpikeFactor = 1.1;

    /// <summary>預設排除的虛擬介面關鍵字（比對名稱與描述，不分大小寫）。</summary>
    public static readonly IReadOnlyList<string> VirtualPatterns =
    [
        "*Hyper-V*", "*Virtual*", "*VPN*", "*TAP-*", "*WireGuard*", "*Wintun*", "*WSL*", "*Docker*",
        "*Loopback*", "*Teredo*", "*ISATAP*", "*6to4*", "*Tunnel*", "*VMware*", "*VirtualBox*",
        "*Npcap*", "*WAN Miniport*", "*Tailscale*", "*ZeroTier*", "*Kernel Debug*",
    ];

    private readonly Lock _gate = new();
    private Dictionary<string, NetworkBaseline> _baselines = [];

    /// <inheritdoc />
    public string Name => ModuleNames.Network;

    /// <inheritdoc />
    public bool IsEnabled => settings.Network.Enabled;

    /// <inheritdoc />
    /// <remarks>含篩選結果（舊版 D10：啟動 Log 記錄網路篩選結果）。</remarks>
    public string? SourceDescription
    {
        get
        {
            IReadOnlyList<InterfaceReading> readings;
            try
            {
                readings = source.GetInterfaces();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                return "網路來源：Win32 GetIfTable2（無法呼叫，需要 Windows）";
            }
            catch (Win32Exception ex)
            {
                return $"網路來源：Win32 GetIfTable2（列舉失敗：{ex.Message}）";
            }

            AdapterSelection[] selection = [.. Select(readings, settings.Network)];
            string included = Describe(selection.Where(s => s.Included).Select(s => s.Reading.Name));
            string excluded = Describe(selection.Where(s => !s.Included).Select(s => $"{s.Reading.Name}（{s.Reason}）"));
            return $"網路來源：Win32 GetIfTable2；納入統計：{included}；排除：{excluded}";
        }
    }

    /// <inheritdoc />
    public Task WarmUpAsync(CancellationToken cancellationToken)
    {
        _ = Sample();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken) => Task.FromResult<ModuleResult>(Sample());

    /// <summary>
    /// 篩選規則（v1.4）：篩選層與回送介面一律不列入；符合 excludeNames 排除（優先）；
    /// includeNames 有內容時只納入符合者（不論實體或連線）；否則只納入連線中、且（physicalOnlyByDefault 時）為實體的介面卡。
    /// </summary>
    public static IEnumerable<AdapterSelection> Select(IEnumerable<InterfaceReading> readings, NetworkSettings network)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(network);
        foreach (InterfaceReading r in readings.Where(r => !r.IsHidden))
        {
            string[] texts = [r.Name, r.Description];
            bool physical = r.IsHardwareInterface && r.IsConnectorPresent && !Matches(texts, VirtualPatterns);
            if (Matches(texts, network.ExcludeNames))
            {
                yield return new AdapterSelection(r, physical, false, false, "excludeNames");
            }
            else if (network.IncludeNames.Count > 0)
            {
                bool match = Matches(texts, network.IncludeNames);
                yield return new AdapterSelection(r, physical, match, match, match ? "includeNames" : "not in includeNames");
            }
            else if (!r.IsUp)
            {
                yield return new AdapterSelection(r, physical, false, false, "not connected");
            }
            else if (network.PhysicalOnlyByDefault && !physical)
            {
                yield return new AdapterSelection(r, physical, false, false, "virtual");
            }
            else
            {
                yield return new AdapterSelection(r, physical, true, false, "default");
            }
        }
    }

    /// <summary>由讀數、上次基準與目前單調時間計算結果與新基準（純函式，供測試）。</summary>
    public static (NetworkResult Result, Dictionary<string, NetworkBaseline> Baselines) Calculate(
        IReadOnlyList<InterfaceReading> readings, IReadOnlyDictionary<string, NetworkBaseline> baselines, TimeSpan now, NetworkSettings network)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(baselines);
        ArgumentNullException.ThrowIfNull(network);
        AdapterSelection[] selection = [.. Select(readings, network)];
        var next = new Dictionary<string, NetworkBaseline>();
        var items = new List<NetworkAdapterItem>();

        foreach (AdapterSelection s in selection.Where(s => s.Included))
        {
            InterfaceReading r = s.Reading;
            if (!r.IsUp)
            {
                // 只有 includeNames 明確指定的介面卡會走到這裡；未連線時捨棄基準。
                items.Add(Item(s, MonitorStatus.Unavailable, null, null) with { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "not connected" });
                continue;
            }

            var current = new NetworkBaseline(r.ReceivedBytes, r.SentBytes, now, r.Name, r.Description);
            if (!baselines.TryGetValue(r.Id, out NetworkBaseline? previous) || previous.Name != r.Name || previous.Description != r.Description)
            {
                next[r.Id] = current;
                items.Add(Item(s, MonitorStatus.WarmingUp, null, null));
                continue;
            }

            double seconds = (now - previous.Time).TotalSeconds;
            if (seconds <= 0)
            {
                next[r.Id] = previous;
                items.Add(Item(s, MonitorStatus.WarmingUp, null, null));
                continue;
            }

            next[r.Id] = current;
            if (r.ReceivedBytes < previous.ReceivedBytes || r.SentBytes < previous.SentBytes)
            {
                // 計數器變小 → 視為重設，重新暖機，不顯示負速度。
                items.Add(Item(s, MonitorStatus.WarmingUp, null, null));
                continue;
            }

            double download = (r.ReceivedBytes - previous.ReceivedBytes) * 8d / seconds / 1_000_000;
            double upload = (r.SentBytes - previous.SentBytes) * 8d / seconds / 1_000_000;
            if (IsSpike(download, r.ReceiveLinkSpeed) || IsSpike(upload, r.TransmitLinkSpeed))
            {
                items.Add(Item(s, MonitorStatus.WarmingUp, null, null));
                continue;
            }

            items.Add(Item(s, MonitorStatus.OK, download, upload));
        }

        // includeNames 中完全找不到介面卡的樣式也顯示為 Unavailable，避免指定的介面卡默默消失。
        foreach (string pattern in network.IncludeNames)
        {
            if (!selection.Any(s => s.Explicit && Matches([s.Reading.Name, s.Reading.Description], [pattern])))
            {
                items.Add(new NetworkAdapterItem(MonitorStatus.Unavailable, pattern, null, null, null, null, null, null, null)
                {
                    ErrorCode = ErrorCode.Unavailable,
                    ErrorMessage = "not found",
                });
            }
        }

        if (items.Count == 0)
        {
            var none = new NetworkResult(MonitorStatus.Unavailable, [], null, null) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "沒有符合條件的網路介面卡" };
            return (none, next);
        }

        NetworkAdapterItem[] ok = [.. items.Where(i => i.Status == MonitorStatus.OK)];
        double? totalDown = ok.Length == 0 ? null : ok.Sum(i => i.DownloadMbps!.Value);
        double? totalUp = ok.Length == 0 ? null : ok.Sum(i => i.UploadMbps!.Value);
        return (new NetworkResult(StatusAggregator.Aggregate(items.Select(i => i.Status)), items, totalDown, totalUp), next);
    }

    /// <summary>萬用字元比對（<c>*</c>、<c>?</c>，不分大小寫，整段符合）：任一文字符合任一樣式即為 true。</summary>
    public static bool Matches(IEnumerable<string?> texts, IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(patterns);
        string?[] values = [.. texts];
        foreach (string pattern in patterns.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            string regex = "^" + Regex.Escape(pattern).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal) + "$";
            if (values.Any(t => t is not null && Regex.IsMatch(t, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSpike(double mbps, ulong? linkBitsPerSecond) =>
        linkBitsPerSecond is ulong link && link > 0 && mbps * 1_000_000 > link * SpikeFactor;

    private static NetworkAdapterItem Item(AdapterSelection s, MonitorStatus status, double? download, double? upload)
    {
        InterfaceReading r = s.Reading;
        bool hasCounters = r.IsUp;
        return new NetworkAdapterItem(
            status,
            r.Name,
            r.Description,
            r.Id,
            s.IsPhysical,
            hasCounters ? unchecked((long)r.ReceivedBytes) : null,
            hasCounters ? unchecked((long)r.SentBytes) : null,
            download,
            upload);
    }

    private static string Describe(IEnumerable<string> names)
    {
        string[] list = [.. names];
        return list.Length == 0 ? "（無）" : string.Join("、", list);
    }

    private NetworkResult Sample()
    {
        lock (_gate)
        {
            IReadOnlyList<InterfaceReading> readings;
            try
            {
                readings = source.GetInterfaces();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                return new NetworkResult(MonitorStatus.Unavailable, [], null, null) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "無法呼叫 GetIfTable2（需要 Windows）" };
            }
            catch (Win32Exception ex)
            {
                // 暫時性失敗保留既有基準，恢復後以實際經過時間繼續計算。
                return new NetworkResult(MonitorStatus.Error, [], null, null) { ErrorCode = ErrorCode.TransientError, ErrorMessage = ex.Message };
            }

            (NetworkResult result, Dictionary<string, NetworkBaseline> next) = Calculate(readings, _baselines, clock.Elapsed, settings.Network);
            _baselines = next;
            return result;
        }
    }
}
