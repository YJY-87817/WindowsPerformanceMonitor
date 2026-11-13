using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PerformanceMonitor.Infrastructure.Windows.Pdh;

/// <summary>pdh.dll 的 P/Invoke 宣告。只在 Windows 可用。</summary>
internal static partial class PdhNative
{
    /// <summary>格式化為 double。</summary>
    public const uint FormatDouble = 0x00000200;

    /// <summary>資料有效。</summary>
    public const uint StatusValidData = 0x00000000;

    /// <summary>新資料（有效）。</summary>
    public const uint StatusNewData = 0x00000001;

    /// <summary>計數器狀態：資料無效（例如速率型計數器只有一次取樣）。</summary>
    public const uint StatusInvalidData = 0xC0000BBA;

    /// <summary>回傳值：資料無效。</summary>
    public const uint InvalidData = 0xC0000BC6;

    /// <summary>回傳值：沒有資料。</summary>
    public const uint NoData = 0x800007D5;

    /// <summary>回傳值：計算時分母為負（計數器重設）。</summary>
    public const uint CalcNegativeDenominator = 0x800007D6;

    /// <summary>回傳值：計算時時間基準為負。</summary>
    public const uint CalcNegativeTimebase = 0x800007D7;

    /// <summary>回傳值：計算時數值為負（計數器重設）。</summary>
    public const uint CalcNegativeValue = 0x800007D8;

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint PdhOpenQuery(string? dataSource, nint userData, out SafePdhQueryHandle query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint PdhAddEnglishCounter(SafePdhQueryHandle query, string fullCounterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhCollectQueryData(SafePdhQueryHandle query);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhGetFormattedCounterValue(nint counter, uint format, out uint type, out FormattedValue value);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhCloseQuery(nint query);

    /// <summary>PDH_FMT_COUNTERVALUE（double 版本）。</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct FormattedValue
    {
        [FieldOffset(0)]
        public uint CStatus;

        [FieldOffset(8)]
        public double DoubleValue;
    }
}

/// <summary>PDH 查詢控制代碼；釋放時呼叫 PdhCloseQuery（同時釋放其下所有計數器）。</summary>
internal sealed class SafePdhQueryHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafePdhQueryHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => PdhNative.PdhCloseQuery(handle) == 0;
}
