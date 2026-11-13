using System.Globalization;
using System.Runtime.InteropServices;

namespace PerformanceMonitor.Infrastructure.Windows.Pdh;

/// <summary>一次格式化讀取的結果。</summary>
public enum PdhReadStatus
{
    /// <summary>有效數值。</summary>
    Ok,

    /// <summary>尚無資料：速率型計數器只有一次取樣，或計數器重設後需重新建立基準。</summary>
    NotReady,

    /// <summary>讀取失敗。</summary>
    Failed,
}

/// <summary>計數器讀取值。</summary>
/// <param name="Status">結果。</param>
/// <param name="Value">數值（只有 <see cref="PdhReadStatus.Ok"/> 時有意義）。</param>
/// <param name="ErrorCode">PDH 錯誤碼（失敗時）。</param>
public readonly record struct PdhValue(PdhReadStatus Status, double Value, uint ErrorCode);

/// <summary>萬用字元計數器（例如 <c>\LogicalDisk(*)\…</c>）一次讀取所有執行個體的結果。</summary>
/// <param name="Status">整體結果；<see cref="PdhReadStatus.Ok"/> 時各執行個體另有自己的狀態。</param>
/// <param name="Instances">執行個體名稱 → 讀取值（名稱不分大小寫）。</param>
/// <param name="ErrorCode">PDH 錯誤碼（整體失敗時）。</param>
public sealed record PdhArrayValue(PdhReadStatus Status, IReadOnlyDictionary<string, PdhValue> Instances, uint ErrorCode)
{
    /// <summary>建立有資料的結果。</summary>
    public static PdhArrayValue Ok(IReadOnlyDictionary<string, PdhValue> instances) => new(PdhReadStatus.Ok, instances, 0);

    /// <summary>建立沒有執行個體資料的結果（尚無資料或失敗）。</summary>
    public static PdhArrayValue Empty(PdhReadStatus status, uint errorCode) =>
        new(status, new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase), errorCode);
}

/// <summary>PDH 呼叫失敗。</summary>
public sealed class PdhException : Exception
{
    /// <summary>建立例外。</summary>
    public PdhException(string operation, uint errorCode)
        : base(string.Create(CultureInfo.InvariantCulture, $"{operation} 失敗（PDH 0x{errorCode:X8}）"))
    {
        ErrorCode = errorCode;
    }

    /// <summary>建立例外。</summary>
    public PdhException()
    {
    }

    /// <summary>建立例外。</summary>
    public PdhException(string message)
        : base(message)
    {
    }

    /// <summary>建立例外。</summary>
    public PdhException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>PDH 錯誤碼。</summary>
    public uint ErrorCode { get; }
}

/// <summary>單一計數器。</summary>
public interface IPdhCounter
{
    /// <summary>讀取最近一次收集的格式化數值（double）。</summary>
    PdhValue Read();
}

/// <summary>萬用字元計數器：每次收集後讀取當時存在的所有執行個體（磁碟插拔後自動反映）。</summary>
public interface IPdhCounterArray
{
    /// <summary>讀取最近一次收集的所有執行個體數值（double）。</summary>
    PdhArrayValue ReadArray();
}

/// <summary>
/// 一個 PDH 查詢：加入計數器後，每次 <see cref="Collect"/> 收集所有計數器的原始值；
/// 速率型計數器由 PDH 以兩次原始值與時間戳計算。
/// </summary>
public interface IPdhQuery : IDisposable
{
    /// <summary>以英文名稱加入計數器（<c>PdhAddEnglishCounter</c>，不受系統語系影響）。失敗時擲出 <see cref="PdhException"/>。</summary>
    IPdhCounter AddCounter(string englishPath);

    /// <summary>以英文名稱加入萬用字元計數器（例如 <c>\LogicalDisk(*)\Disk Reads/sec</c>）。失敗時擲出 <see cref="PdhException"/>。</summary>
    IPdhCounterArray AddCounterArray(string englishPath);

    /// <summary>收集資料。失敗時擲出 <see cref="PdhException"/>。</summary>
    void Collect();
}

/// <summary>建立 PDH 查詢（測試以假物件取代）。</summary>
public interface IPdhQueryFactory
{
    /// <summary>建立新的查詢。失敗時擲出 <see cref="PdhException"/>。</summary>
    IPdhQuery Create();
}

/// <summary>以 pdh.dll 實作的查詢工廠。</summary>
public sealed class PdhQueryFactory : IPdhQueryFactory
{
    /// <inheritdoc />
    public IPdhQuery Create() => new PdhQuery();
}

/// <summary>以 pdh.dll 實作的查詢。</summary>
internal sealed class PdhQuery : IPdhQuery
{
    private readonly SafePdhQueryHandle _handle;

    public PdhQuery()
    {
        uint status = PdhNative.PdhOpenQuery(null, 0, out _handle);
        if (status != 0)
        {
            _handle.Dispose();
            throw new PdhException("PdhOpenQuery", status);
        }
    }

    public IPdhCounter AddCounter(string englishPath)
    {
        uint status = PdhNative.PdhAddEnglishCounter(_handle, englishPath, 0, out nint counter);
        return status == 0 ? new Counter(counter, _handle) : throw new PdhException($"加入計數器 {englishPath}", status);
    }

    public IPdhCounterArray AddCounterArray(string englishPath)
    {
        uint status = PdhNative.PdhAddEnglishCounter(_handle, englishPath, 0, out nint counter);
        return status == 0 ? new CounterArray(counter, _handle) : throw new PdhException($"加入計數器 {englishPath}", status);
    }

    public void Collect()
    {
        uint status = PdhNative.PdhCollectQueryData(_handle);
        if (status != 0 && status != PdhNative.NoData)
        {
            throw new PdhException("PdhCollectQueryData", status);
        }
    }

    public void Dispose() => _handle.Dispose();

    private sealed class Counter(nint handle, SafePdhQueryHandle owner) : IPdhCounter
    {
        public PdhValue Read()
        {
            ObjectDisposedException.ThrowIf(owner.IsClosed, this);
            uint status = PdhNative.PdhGetFormattedCounterValue(handle, PdhNative.FormatDouble, out _, out PdhNative.FormattedValue value);
            if (IsNotReady(status))
            {
                return new PdhValue(PdhReadStatus.NotReady, 0, status);
            }

            return status != 0 ? new PdhValue(PdhReadStatus.Failed, 0, status) : FromFormatted(value);
        }
    }

    private sealed class CounterArray(nint handle, SafePdhQueryHandle owner) : IPdhCounterArray
    {
        private const int MaxAttempts = 3;

        public PdhArrayValue ReadArray()
        {
            ObjectDisposedException.ThrowIf(owner.IsClosed, this);

            // 第一次呼叫取得所需大小；執行個體在兩次呼叫之間增加時會再回 MoreData，重試幾次。
            uint size = 0;
            uint status = PdhNative.PdhGetFormattedCounterArray(handle, PdhNative.FormatDouble, ref size, out _, 0);
            for (int attempt = 0; attempt < MaxAttempts && status == PdhNative.MoreData; attempt++)
            {
                nint buffer = Marshal.AllocHGlobal((nint)size);
                try
                {
                    status = PdhNative.PdhGetFormattedCounterArray(handle, PdhNative.FormatDouble, ref size, out uint count, buffer);
                    if (status == 0)
                    {
                        return PdhArrayValue.Ok(Parse(buffer, count));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            if (status == 0)
            {
                // 沒有任何執行個體。
                return PdhArrayValue.Ok(new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase));
            }

            return PdhArrayValue.Empty(IsNotReady(status) ? PdhReadStatus.NotReady : PdhReadStatus.Failed, status);
        }

        private static Dictionary<string, PdhValue> Parse(nint buffer, uint count)
        {
            var result = new Dictionary<string, PdhValue>(StringComparer.OrdinalIgnoreCase);
            int itemSize = Marshal.SizeOf<PdhNative.FormattedItem>();
            for (int i = 0; i < count; i++)
            {
                PdhNative.FormattedItem item = Marshal.PtrToStructure<PdhNative.FormattedItem>(buffer + (i * itemSize));
                string? name = Marshal.PtrToStringUni(item.Name);
                if (name is not null)
                {
                    result.TryAdd(name, FromFormatted(item.Value));
                }
            }

            return result;
        }
    }

    private static bool IsNotReady(uint status) =>
        status is PdhNative.InvalidData or PdhNative.NoData or PdhNative.CalcNegativeDenominator
            or PdhNative.CalcNegativeTimebase or PdhNative.CalcNegativeValue;

    private static PdhValue FromFormatted(PdhNative.FormattedValue value) => value.CStatus switch
    {
        PdhNative.StatusValidData or PdhNative.StatusNewData => new PdhValue(PdhReadStatus.Ok, value.DoubleValue, 0),
        PdhNative.StatusInvalidData => new PdhValue(PdhReadStatus.NotReady, 0, value.CStatus),
        _ => new PdhValue(PdhReadStatus.Failed, 0, value.CStatus),
    };
}
