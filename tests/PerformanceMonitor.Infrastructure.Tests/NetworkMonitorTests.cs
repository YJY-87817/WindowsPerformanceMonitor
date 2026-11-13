using System.ComponentModel;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Sensors;
using PerformanceMonitor.Infrastructure.Windows.Network;

namespace PerformanceMonitor.Infrastructure.Tests;

/// <summary>移植舊版 Pester NetworkMonitor.Tests.ps1 的案例（篩選、速度、重設、尖峰、合計、指定介面卡）。</summary>
public class NetworkMonitorTests
{
    private const ulong Gbps = 1_000_000_000;

    private static readonly NetworkSettings Defaults = new();

    private static InterfaceReading Nic(
        string name,
        string description = "Intel(R) Ethernet Connection",
        bool hardware = true,
        bool connector = true,
        bool up = true,
        bool hidden = false,
        ulong received = 0,
        ulong sent = 0,
        ulong? link = Gbps,
        string? id = null) =>
        new(id ?? "{" + name + "}", name, description, hidden, hardware, connector, up, received, sent, link, link);

    private static (NetworkResult Result, Dictionary<string, NetworkBaseline> Baselines) Run(
        IReadOnlyList<InterfaceReading> readings, IReadOnlyDictionary<string, NetworkBaseline>? baselines = null, double seconds = 0, NetworkSettings? network = null) =>
        NetworkMonitor.Calculate(readings, baselines ?? new Dictionary<string, NetworkBaseline>(), TimeSpan.FromSeconds(seconds), network ?? Defaults);

    /// <summary>兩次取樣：第一次 t=0 建立基準，第二次 t=秒數。</summary>
    private static NetworkResult Twice(InterfaceReading first, InterfaceReading second, double seconds = 2, NetworkSettings? network = null)
    {
        (_, Dictionary<string, NetworkBaseline> baselines) = Run([first], network: network);
        return Run([second], baselines, seconds, network).Result;
    }

    [Fact]
    public void DefaultIncludesOnlyConnectedPhysicalAdapters()
    {
        AdapterSelection[] s =
        [
            .. NetworkMonitor.Select(
                [
                    Nic("Ethernet"),
                    Nic("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter"),
                    Nic("Wi-Fi", "Intel(R) Wi-Fi 6", up: false),
                    Nic("Bluetooth", "Bluetooth Device", connector: false),
                    Nic("Ethernet-WFP Native MAC Layer LightWeight Filter-0000", hidden: true),
                    Nic("Loopback Pseudo-Interface 1", hidden: true),
                ],
                Defaults),
        ];

        Assert.Equal(["Ethernet"], s.Where(x => x.Included).Select(x => x.Reading.Name));
        Assert.Equal(["default", "virtual", "not connected", "virtual"], s.Select(x => x.Reason));
        Assert.DoesNotContain(s, x => x.Reading.IsHidden);
    }

    [Fact]
    public void HardwareAdapterWithVirtualKeywordIsVirtual()
    {
        AdapterSelection s = Assert.Single(NetworkMonitor.Select([Nic("Ethernet 2", "Microsoft Hyper-V Network Adapter")], Defaults));

        Assert.False(s.IsPhysical);
        Assert.False(s.Included);
    }

    [Fact]
    public void PhysicalOnlyFalseIncludesAllConnected()
    {
        var network = new NetworkSettings { PhysicalOnlyByDefault = false };

        AdapterSelection[] s = [.. NetworkMonitor.Select([Nic("Ethernet"), Nic("vEthernet", "Hyper-V Virtual"), Nic("Wi-Fi", up: false)], network)];

        Assert.Equal(["Ethernet", "vEthernet"], s.Where(x => x.Included).Select(x => x.Reading.Name));
    }

    [Fact]
    public void IncludeNamesReplaceDefaultWithWildcardsOnNameOrDescriptionIgnoringCase()
    {
        var network = new NetworkSettings { IncludeNames = ["wi-?i", "*HYPER-V*"] };

        AdapterSelection[] s = [.. NetworkMonitor.Select([Nic("Ethernet"), Nic("Wi-Fi", up: false), Nic("vEthernet", "Hyper-V Virtual")], network)];

        Assert.Equal(["Wi-Fi", "vEthernet"], s.Where(x => x.Included).Select(x => x.Reading.Name));
        Assert.All(s.Where(x => x.Included), x => Assert.True(x.Explicit));
        Assert.Equal("not in includeNames", s[0].Reason);
    }

    [Fact]
    public void ExcludeNamesWinOverIncludeNames()
    {
        var network = new NetworkSettings { IncludeNames = ["*"], ExcludeNames = ["Ethernet"] };

        AdapterSelection[] s = [.. NetworkMonitor.Select([Nic("Ethernet"), Nic("Wi-Fi")], network)];

        Assert.Equal(["Wi-Fi"], s.Where(x => x.Included).Select(x => x.Reading.Name));
        Assert.Equal("excludeNames", s[0].Reason);
    }

    [Fact]
    public void HiddenInterfacesAreNeverIncludedEvenWithStar()
    {
        var network = new NetworkSettings { IncludeNames = ["*"] };

        Assert.Empty(NetworkMonitor.Select([Nic("Ethernet-WFP Native MAC Layer LightWeight Filter-0000", hidden: true)], network));
    }

    [Theory]
    [InlineData("Ethernet", "ethernet", true)]
    [InlineData("Ethernet 2", "Ethernet", false)] // 整段符合
    [InlineData("Ethernet 2", "Ethernet*", true)]
    [InlineData("Wi-Fi", "W?-Fi", true)]
    [InlineData("a.b", "a?b", true)]
    [InlineData("axb", "a.b", false)] // 其他字元照字面比對
    public void WildcardMatching(string text, string pattern, bool expected) =>
        Assert.Equal(expected, NetworkMonitor.Matches([text], [pattern]));

    [Fact]
    public void FirstSampleOnlyBuildsBaselineWithoutZeroSpeed()
    {
        NetworkAdapterItem item = Assert.Single(Run([Nic("Ethernet", received: 1000, sent: 500)]).Result.Adapters);

        Assert.Equal(MonitorStatus.WarmingUp, item.Status);
        Assert.Null(item.DownloadMbps);
        Assert.Null(item.UploadMbps);
        Assert.Equal(1000, item.ReceivedBytes);
    }

    [Fact]
    public void CalculatesMbpsFromElapsedTime()
    {
        // 2 秒內收 2,500,000 bytes → 10 Mbps；送 250,000 bytes → 1 Mbps
        NetworkResult result = Twice(Nic("Ethernet", received: 1000, sent: 1000), Nic("Ethernet", received: 2_501_000, sent: 251_000));

        NetworkAdapterItem item = Assert.Single(result.Adapters);
        Assert.Equal(MonitorStatus.OK, item.Status);
        Assert.Equal(10, item.DownloadMbps!.Value, 9);
        Assert.Equal(1, item.UploadMbps!.Value, 9);
        Assert.Equal(10, result.TotalDownloadMbps!.Value, 9);
        Assert.Equal(MonitorStatus.OK, result.Status);
    }

    [Fact]
    public void NoTrafficIsRealZero()
    {
        NetworkAdapterItem item = Assert.Single(Twice(Nic("Ethernet", received: 5, sent: 5), Nic("Ethernet", received: 5, sent: 5)).Adapters);

        Assert.Equal(MonitorStatus.OK, item.Status);
        Assert.Equal(0, item.DownloadMbps);
    }

    [Fact]
    public void CounterDecreaseIsResetWithoutNegativeSpeed()
    {
        NetworkAdapterItem item = Assert.Single(Twice(Nic("Ethernet", received: 5000, sent: 10), Nic("Ethernet", received: 100, sent: 20)).Adapters);

        Assert.Equal(MonitorStatus.WarmingUp, item.Status);
        Assert.Null(item.DownloadMbps);
    }

    [Fact]
    public void IdentityChangeRewarms()
    {
        NetworkAdapterItem item = Assert.Single(Twice(Nic("Ethernet", "Old NIC", id: "{1}"), Nic("Ethernet", "New NIC", id: "{1}", received: 100)).Adapters);

        Assert.Equal(MonitorStatus.WarmingUp, item.Status);
    }

    [Fact]
    public void SpikeAboveLinkSpeedIsReset()
    {
        // 100 Mbps 連線，2 秒內收 30,000,000 bytes = 120 Mbps > 110 Mbps
        NetworkAdapterItem item = Assert.Single(Twice(Nic("Ethernet", link: 100_000_000), Nic("Ethernet", link: 100_000_000, received: 30_000_000)).Adapters);

        Assert.Equal(MonitorStatus.WarmingUp, item.Status);
    }

    [Fact]
    public void SpeedWithin110PercentIsAccepted()
    {
        // 2 秒內收 27,000,000 bytes = 108 Mbps ≤ 110 Mbps
        NetworkAdapterItem item = Assert.Single(Twice(Nic("Ethernet", link: 100_000_000), Nic("Ethernet", link: 100_000_000, received: 27_000_000)).Adapters);

        Assert.Equal(MonitorStatus.OK, item.Status);
    }

    [Fact]
    public void UnknownLinkSpeedSkipsSpikeCheck()
    {
        NetworkAdapterItem item = Assert.Single(Twice(Nic("Ethernet", link: null), Nic("Ethernet", link: null, received: 30_000_000_000)).Adapters);

        Assert.Equal(MonitorStatus.OK, item.Status);
    }

    [Fact]
    public void ZeroElapsedKeepsOriginalBaseline()
    {
        (_, Dictionary<string, NetworkBaseline> first) = Run([Nic("Ethernet", received: 0)]);
        (NetworkResult same, Dictionary<string, NetworkBaseline> kept) = Run([Nic("Ethernet", received: 1000)], first, 0);
        NetworkResult later = Run([Nic("Ethernet", received: 250_000)], kept, 2).Result;

        Assert.Equal(MonitorStatus.WarmingUp, Assert.Single(same.Adapters).Status);
        Assert.Equal(first["{Ethernet}"], kept["{Ethernet}"]);
        Assert.Equal(1, Assert.Single(later.Adapters).DownloadMbps!.Value, 9); // 從 t=0、0 bytes 起算
    }

    [Fact]
    public void TotalsOnlyOkAdapters()
    {
        var network = new NetworkSettings { PhysicalOnlyByDefault = false };
        (_, Dictionary<string, NetworkBaseline> b) = Run([Nic("A", received: 0), Nic("B", received: 0)], network: network);

        NetworkResult result = Run([Nic("A", received: 250_000), Nic("B", received: 0), Nic("C", received: 99)], b, 2, network).Result;

        Assert.Equal(["A", "B", "C"], result.Adapters.Select(a => a.AdapterName));
        Assert.Equal(MonitorStatus.WarmingUp, result.Adapters[2].Status);
        Assert.Equal(1, result.TotalDownloadMbps!.Value, 9);
        Assert.Equal(MonitorStatus.OK, result.Status);
    }

    [Fact]
    public void NoOkAdapterLeavesTotalsEmpty()
    {
        NetworkResult result = Run([Nic("Ethernet")]).Result;

        Assert.Null(result.TotalDownloadMbps);
        Assert.Null(result.TotalUploadMbps);
        Assert.Equal(MonitorStatus.WarmingUp, result.Status);
    }

    [Fact]
    public void NoMatchingAdapterIsUnavailable()
    {
        NetworkResult result = Run([Nic("vEthernet", "Hyper-V Virtual")]).Result;

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
        Assert.Equal(ErrorCode.Unavailable, result.ErrorCode);
        Assert.Empty(result.Adapters);
    }

    [Fact]
    public void IncludedButDisconnectedOrMissingAdaptersAreUnavailable()
    {
        var network = new NetworkSettings { IncludeNames = ["Ethernet", "Wi-Fi", "USB*"] };
        (_, Dictionary<string, NetworkBaseline> b) = Run([Nic("Ethernet"), Nic("Wi-Fi", up: false)], network: network);

        NetworkResult result = Run([Nic("Ethernet", received: 250_000), Nic("Wi-Fi", up: false)], b, 2, network).Result;

        Assert.Equal(["Ethernet", "Wi-Fi", "USB*"], result.Adapters.Select(a => a.AdapterName));
        Assert.Equal(MonitorStatus.OK, result.Adapters[0].Status);
        Assert.Equal("not connected", result.Adapters[1].ErrorMessage);
        Assert.Null(result.Adapters[1].ReceivedBytes);
        Assert.Equal("not found", result.Adapters[2].ErrorMessage);
        Assert.Equal(MonitorStatus.OK, result.Status);
    }

    [Fact]
    public void ReconnectRewarms()
    {
        var network = new NetworkSettings { IncludeNames = ["Wi-Fi"] };
        (_, Dictionary<string, NetworkBaseline> b1) = Run([Nic("Wi-Fi", received: 0)], network: network);
        (NetworkResult down, Dictionary<string, NetworkBaseline> b2) = Run([Nic("Wi-Fi", up: false)], b1, 2, network);
        NetworkResult back = Run([Nic("Wi-Fi", received: 1_000_000)], b2, 4, network).Result;

        Assert.Equal(MonitorStatus.Unavailable, Assert.Single(down.Adapters).Status);
        Assert.Empty(b2);
        Assert.Equal(MonitorStatus.WarmingUp, Assert.Single(back.Adapters).Status);
    }

    private sealed class FakeSource : INetworkInterfaceSource
    {
        public Func<IReadOnlyList<InterfaceReading>> Next { get; set; } = () => [];

        public int Calls { get; private set; }

        public IReadOnlyList<InterfaceReading> GetInterfaces()
        {
            Calls++;
            return Next();
        }
    }

    [Fact]
    public async Task WarmUpBuildsBaselineSoFirstSampleHasSpeed()
    {
        var source = new FakeSource { Next = () => [Nic("Ethernet", received: 0)] };
        var clock = new FakeClock();
        var monitor = new NetworkMonitor(new AppSettings(), source, clock);

        await monitor.WarmUpAsync(CancellationToken.None);
        clock.Elapsed += TimeSpan.FromSeconds(1);
        source.Next = () => [Nic("Ethernet", received: 125_000)];
        var result = Assert.IsType<NetworkResult>(await monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.OK, result.Status);
        Assert.Equal(1, result.TotalDownloadMbps!.Value, 9);
    }

    [Fact]
    public async Task QueryFailureIsErrorAndKeepsBaseline()
    {
        var source = new FakeSource { Next = () => [Nic("Ethernet", received: 0)] };
        var clock = new FakeClock();
        var monitor = new NetworkMonitor(new AppSettings(), source, clock);
        await monitor.SampleAsync(CancellationToken.None);

        clock.Elapsed += TimeSpan.FromSeconds(2);
        source.Next = () => throw new Win32Exception(87, "參數錯誤");
        var failed = Assert.IsType<NetworkResult>(await monitor.SampleAsync(CancellationToken.None));
        clock.Elapsed += TimeSpan.FromSeconds(2);
        source.Next = () => [Nic("Ethernet", received: 500_000)];
        var recovered = Assert.IsType<NetworkResult>(await monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Error, failed.Status);
        Assert.Equal(ErrorCode.TransientError, failed.ErrorCode);
        Assert.Equal(MonitorStatus.OK, recovered.Status);
        Assert.Equal(1, recovered.TotalDownloadMbps!.Value, 9); // 500,000 bytes × 8 ÷ 4 秒
    }

    [Fact]
    public async Task MissingIphlpapiIsUnavailable()
    {
        var source = new FakeSource { Next = () => throw new DllNotFoundException("iphlpapi.dll") };
        var monitor = new NetworkMonitor(new AppSettings(), source, new FakeClock());

        var result = Assert.IsType<NetworkResult>(await monitor.SampleAsync(CancellationToken.None));

        Assert.Equal(MonitorStatus.Unavailable, result.Status);
    }

    [Fact]
    public void SourceDescriptionListsSelection()
    {
        var source = new FakeSource { Next = () => [Nic("Ethernet"), Nic("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter"), Nic("Filter", hidden: true)] };
        var monitor = new NetworkMonitor(new AppSettings(), source, new FakeClock());

        Assert.Equal("網路來源：Win32 GetIfTable2；納入統計：Ethernet；排除：vEthernet (WSL)（virtual）", monitor.SourceDescription);

        source.Next = () => [];
        Assert.Equal("網路來源：Win32 GetIfTable2；納入統計：（無）；排除：（無）", monitor.SourceDescription);

        source.Next = () => throw new Win32Exception(5, "拒絕存取");
        Assert.Contains("列舉失敗：拒絕存取", monitor.SourceDescription, StringComparison.Ordinal);

        source.Next = () => throw new DllNotFoundException("long loader message");
        Assert.Equal("網路來源：Win32 GetIfTable2（無法呼叫，需要 Windows）", monitor.SourceDescription);
    }

    [Fact]
    public void FollowsSettings()
    {
        var monitor = new NetworkMonitor(new AppSettings { Network = new NetworkSettings { Enabled = false } }, new FakeSource(), new FakeClock());

        Assert.False(monitor.IsEnabled);
        Assert.Equal(ModuleNames.Network, monitor.Name);
    }

    [Fact]
    public void MibIfRow2LayoutMatchesNetioapi()
    {
        // netioapi.h 的 MIB_IF_ROW2（x64）：大小 1352 位元組；以位移檢查結構對應正確。
        IReadOnlyDictionary<string, int> layout = IfTableSource.Layout();

        Assert.Equal(1352, layout["Size"]);
        Assert.Equal(12, layout["InterfaceGuid"]);
        Assert.Equal(28, layout["Alias"]);
        Assert.Equal(542, layout["Description"]);
        Assert.Equal(1128, layout["Type"]);
        Assert.Equal(1152, layout["InterfaceAndOperStatusFlags"]);
        Assert.Equal(1156, layout["OperStatus"]);
        Assert.Equal(1192, layout["TransmitLinkSpeed"]);
        Assert.Equal(1200, layout["ReceiveLinkSpeed"]);
        Assert.Equal(1208, layout["InOctets"]);
        Assert.Equal(1280, layout["OutOctets"]);
        Assert.Equal(8, IfTableSource.RowsOffset);
    }
}
