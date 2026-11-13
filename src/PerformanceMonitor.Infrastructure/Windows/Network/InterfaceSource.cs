using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PerformanceMonitor.Infrastructure.Windows.Network;

/// <summary>單一網路介面讀數（GetIfTable2 的一列）。</summary>
/// <param name="Id">穩定識別值（InterfaceGuid）。</param>
/// <param name="Name">介面卡名稱（Alias，例如 <c>Ethernet</c>、<c>Wi-Fi</c>）。</param>
/// <param name="Description">描述（驅動程式名稱）。</param>
/// <param name="IsHidden">篩選層介面（FilterInterface）或回送介面；一律不列入（取代舊版 Hidden）。</param>
/// <param name="IsHardwareInterface">HardwareInterface 旗標。</param>
/// <param name="IsConnectorPresent">ConnectorPresent 旗標（有實體接頭）。</param>
/// <param name="IsUp">OperStatus 為 Up。</param>
/// <param name="ReceivedBytes">累積接收位元組（InOctets）。</param>
/// <param name="SentBytes">累積傳送位元組（OutOctets）。</param>
/// <param name="ReceiveLinkSpeed">接收連線速度（bits/s）；未知為 null。</param>
/// <param name="TransmitLinkSpeed">傳送連線速度（bits/s）；未知為 null。</param>
public sealed record InterfaceReading(
    string Id,
    string Name,
    string Description,
    bool IsHidden,
    bool IsHardwareInterface,
    bool IsConnectorPresent,
    bool IsUp,
    ulong ReceivedBytes,
    ulong SentBytes,
    ulong? ReceiveLinkSpeed,
    ulong? TransmitLinkSpeed);

/// <summary>列出網路介面（測試以假物件取代）。</summary>
public interface INetworkInterfaceSource
{
    /// <summary>列出所有介面。失敗時擲出 <see cref="Win32Exception"/>。</summary>
    IReadOnlyList<InterfaceReading> GetInterfaces();
}

/// <summary>以 Win32 <c>GetIfTable2</c>（iphlpapi.dll）實作，不需 WMI（盤點 M7）。</summary>
public sealed unsafe partial class IfTableSource : INetworkInterfaceSource
{
    /// <summary>IF_TYPE_SOFTWARE_LOOPBACK。</summary>
    internal const uint TypeSoftwareLoopback = 24;

    /// <summary>IfOperStatusUp。</summary>
    internal const uint OperStatusUp = 1;

    private const byte FlagHardwareInterface = 0x01;
    private const byte FlagFilterInterface = 0x02;
    private const byte FlagConnectorPresent = 0x04;

    /// <inheritdoc />
    public IReadOnlyList<InterfaceReading> GetInterfaces()
    {
        uint status = GetIfTable2(out nint table);
        if (status != 0)
        {
            throw new Win32Exception((int)status, $"GetIfTable2 失敗（{status}）");
        }

        try
        {
            uint count = *(uint*)table;
            var rows = (MibIfRow2*)(table + RowsOffset);
            var result = new List<InterfaceReading>((int)count);
            for (int i = 0; i < count; i++)
            {
                result.Add(ToReading(&rows[i]));
            }

            return result;
        }
        finally
        {
            FreeMibTable(table);
        }
    }

    /// <summary>MIB_IF_TABLE2 中第一列的位移（NumEntries 之後依 8 位元組對齊）。</summary>
    internal static int RowsOffset => 8;

    /// <summary>MIB_IF_ROW2 的大小與主要欄位位移（測試核對 netioapi.h 用）。</summary>
    internal static IReadOnlyDictionary<string, int> Layout()
    {
        MibIfRow2 row = default;
        byte* start = (byte*)&row;
        return new Dictionary<string, int>
        {
            ["Size"] = sizeof(MibIfRow2),
            [nameof(MibIfRow2.InterfaceGuid)] = (int)((byte*)&row.InterfaceGuid - start),
            [nameof(MibIfRow2.Alias)] = (int)((byte*)row.Alias - start),
            [nameof(MibIfRow2.Description)] = (int)((byte*)row.Description - start),
            [nameof(MibIfRow2.Type)] = (int)((byte*)&row.Type - start),
            [nameof(MibIfRow2.InterfaceAndOperStatusFlags)] = (int)((byte*)&row.InterfaceAndOperStatusFlags - start),
            [nameof(MibIfRow2.OperStatus)] = (int)((byte*)&row.OperStatus - start),
            [nameof(MibIfRow2.TransmitLinkSpeed)] = (int)((byte*)&row.TransmitLinkSpeed - start),
            [nameof(MibIfRow2.ReceiveLinkSpeed)] = (int)((byte*)&row.ReceiveLinkSpeed - start),
            [nameof(MibIfRow2.InOctets)] = (int)((byte*)&row.InOctets - start),
            [nameof(MibIfRow2.OutOctets)] = (int)((byte*)&row.OutOctets - start),
        };
    }

    private static InterfaceReading ToReading(MibIfRow2* row)
    {
        byte flags = row->InterfaceAndOperStatusFlags;
        return new InterfaceReading(
            row->InterfaceGuid.ToString("B"),
            new string(row->Alias),
            new string(row->Description),
            IsHidden: (flags & FlagFilterInterface) != 0 || row->Type == TypeSoftwareLoopback,
            IsHardwareInterface: (flags & FlagHardwareInterface) != 0,
            IsConnectorPresent: (flags & FlagConnectorPresent) != 0,
            IsUp: row->OperStatus == OperStatusUp,
            row->InOctets,
            row->OutOctets,
            LinkSpeed(row->ReceiveLinkSpeed),
            LinkSpeed(row->TransmitLinkSpeed));
    }

    private static ulong? LinkSpeed(ulong value) => value is 0 or ulong.MaxValue ? null : value;

    [LibraryImport("iphlpapi.dll")]
    private static partial uint GetIfTable2(out nint table);

    [LibraryImport("iphlpapi.dll")]
    private static partial void FreeMibTable(nint memory);

    /// <summary>MIB_IF_ROW2（netioapi.h），x64 大小 1352 位元組。</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MibIfRow2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        public fixed char Alias[257];
        public fixed char Description[257];
        public uint PhysicalAddressLength;
        public fixed byte PhysicalAddress[32];
        public fixed byte PermanentPhysicalAddress[32];
        public uint Mtu;
        public uint Type;
        public uint TunnelType;
        public uint MediaType;
        public uint PhysicalMediumType;
        public uint AccessType;
        public uint DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public uint OperStatus;
        public uint AdminStatus;
        public uint MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets;
        public ulong InUcastPkts;
        public ulong InNUcastPkts;
        public ulong InDiscards;
        public ulong InErrors;
        public ulong InUnknownProtos;
        public ulong InUcastOctets;
        public ulong InMulticastOctets;
        public ulong InBroadcastOctets;
        public ulong OutOctets;
        public ulong OutUcastPkts;
        public ulong OutNUcastPkts;
        public ulong OutDiscards;
        public ulong OutErrors;
        public ulong OutUcastOctets;
        public ulong OutMulticastOctets;
        public ulong OutBroadcastOctets;
        public ulong OutQLen;
    }
}
