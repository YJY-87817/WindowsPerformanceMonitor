using System.Runtime.InteropServices;

namespace PerformanceMonitor.Infrastructure.Windows.Memory;

/// <summary>實體記憶體讀數（位元組）。</summary>
/// <param name="TotalBytes">實體記憶體總量（ullTotalPhys）。</param>
/// <param name="AvailableBytes">可用實體記憶體（ullAvailPhys，含待命清單，與工作管理員「可用」一致）。</param>
public readonly record struct MemoryReading(ulong TotalBytes, ulong AvailableBytes);

/// <summary>讀取實體記憶體狀態（測試以假物件取代）。</summary>
public interface IMemoryStatusSource
{
    /// <summary>讀取一次。失敗時擲出 <see cref="System.ComponentModel.Win32Exception"/>。</summary>
    MemoryReading Read();
}

/// <summary>以 Win32 <c>GlobalMemoryStatusEx</c> 實作。</summary>
public sealed partial class GlobalMemoryStatusSource : IMemoryStatusSource
{
    /// <inheritdoc />
    public MemoryReading Read()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new MemoryReading(status.TotalPhys, status.AvailPhys);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
