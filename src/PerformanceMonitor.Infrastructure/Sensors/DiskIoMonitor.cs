using System.Globalization;
using System.Text.RegularExpressions;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Disk;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>一種磁碟物件（LogicalDisk 或 PhysicalDisk）在一次收集中的 7 個萬用字元計數器讀值。</summary>
/// <param name="ReadBytes"><c>Disk Read Bytes/sec</c>。</param>
/// <param name="WriteBytes"><c>Disk Write Bytes/sec</c>。</param>
/// <param name="Reads"><c>Disk Reads/sec</c>。</param>
/// <param name="Writes"><c>Disk Writes/sec</c>。</param>
/// <param name="SecPerRead"><c>Avg. Disk sec/Read</c>。</param>
/// <param name="SecPerWrite"><c>Avg. Disk sec/Write</c>。</param>
/// <param name="IdleTime"><c>% Idle Time</c>。</param>
public sealed record DiskIoCounterSet(
    PdhArrayValue ReadBytes,
    PdhArrayValue WriteBytes,
    PdhArrayValue Reads,
    PdhArrayValue Writes,
    PdhArrayValue SecPerRead,
    PdhArrayValue SecPerWrite,
    PdhArrayValue IdleTime)
{
    /// <summary>計數器名稱，順序與建構參數相同。</summary>
    public static IReadOnlyList<string> CounterNames { get; } =
        ["Disk Read Bytes/sec", "Disk Write Bytes/sec", "Disk Reads/sec", "Disk Writes/sec", "Avg. Disk sec/Read", "Avg. Disk sec/Write", "% Idle Time"];

    /// <summary>依 <see cref="CounterNames"/> 順序列出。</summary>
    public IReadOnlyList<PdhArrayValue> All => [ReadBytes, WriteBytes, Reads, Writes, SecPerRead, SecPerWrite, IdleTime];

    /// <summary>依 <see cref="CounterNames"/> 順序建立。</summary>
    public static DiskIoCounterSet From(IReadOnlyList<PdhArrayValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
    }
}

/// <summary>
/// 磁碟 I/O：PDH 萬用字元計數器 <c>\LogicalDisk(*)\…</c> 與 <c>\PhysicalDisk(*)\…</c>（英文名稱加入）。
/// 邏輯磁碟只列固定磁碟（與磁碟容量模組相同）；固定磁碟沒有計數器資料時該磁碟為 <c>Unavailable</c>。
/// 實體磁碟列出 <c>_Total</c> 以外的所有執行個體，名稱沿用 Windows（例如 <c>0 C: D:</c>）。
/// 速率型計數器第一次讀取為 <c>WarmingUp</c>；PDH 失敗時回報 <c>Error</c> 並在下次取樣重新建立查詢。
/// </summary>
public sealed partial class DiskIoMonitor(AppSettings settings, IPdhQueryFactory factory, IDriveSource drives) : IMonitor, IDisposable
{
    private const double BytesPerMB = 1_048_576d;
    private const string TotalInstance = "_Total";

    private readonly Lock _gate = new();
    private IPdhQuery? _query;
    private IPdhCounterArray[]? _logical;
    private IPdhCounterArray[]? _physical;

    /// <inheritdoc />
    public string Name => ModuleNames.DiskIo;

    /// <inheritdoc />
    public bool IsEnabled => settings.DiskIo.Enabled;

    /// <inheritdoc />
    public string? SourceDescription => @"磁碟 I/O 來源：PDH \LogicalDisk(*)、\PhysicalDisk(*)（英文名稱加入）";

    /// <summary>計數器完整路徑，例如 <c>\LogicalDisk(*)\Disk Reads/sec</c>。</summary>
    public static string CounterPath(DiskIoScope scope, string counter) =>
        $@"\{(scope == DiskIoScope.Logical ? "LogicalDisk" : "PhysicalDisk")}(*)\{counter}";

    /// <inheritdoc />
    public Task WarmUpAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            try
            {
                EnsureQuery().Collect();
            }
            catch (Exception ex) when (ex is PdhException or DllNotFoundException or EntryPointNotFoundException)
            {
                Reset();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<ModuleResult>(Sample());
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            Reset();
        }
    }

    /// <summary>
    /// 由計數器讀值計算結果（純函式，供測試）。
    /// </summary>
    /// <param name="fixedDrives">固定磁碟代號；無法列舉時為 null，改用計數器中符合 <c>X:</c> 的執行個體。</param>
    /// <param name="logical">LogicalDisk 讀值。</param>
    /// <param name="physical">PhysicalDisk 讀值。</param>
    public static DiskIoResult Calculate(IReadOnlyList<string>? fixedDrives, DiskIoCounterSet logical, DiskIoCounterSet physical)
    {
        ArgumentNullException.ThrowIfNull(logical);
        ArgumentNullException.ThrowIfNull(physical);

        IEnumerable<string> logicalNames = fixedDrives
            ?? logical.All.SelectMany(a => a.Instances.Keys).Where(n => DriveLetter().IsMatch(n));
        string[] sortedLogical = [.. logicalNames.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

        string[] sortedPhysical =
        [
            .. physical.All.SelectMany(a => a.Instances.Keys)
                .Where(n => !string.Equals(n, TotalInstance, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(PhysicalIndex)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase),
        ];

        DiskIoItem[] items =
        [
            .. sortedLogical.Select(n => Item(DiskIoScope.Logical, n, logical)),
            .. sortedPhysical.Select(n => Item(DiskIoScope.Physical, n, physical)),
        ];

        if (items.Length == 0)
        {
            PdhArrayValue? failed = logical.All.Concat(physical.All).FirstOrDefault(a => a.Status == PdhReadStatus.Failed);
            if (failed is not null)
            {
                return new DiskIoResult(MonitorStatus.Error, [])
                {
                    ErrorCode = ErrorCode.DataError,
                    ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"讀取磁碟計數器失敗（PDH 0x{failed.ErrorCode:X8}）"),
                };
            }

            return logical.All.Concat(physical.All).Any(a => a.Status == PdhReadStatus.NotReady)
                ? new DiskIoResult(MonitorStatus.WarmingUp, [])
                : new DiskIoResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "沒有磁碟 I/O 計數器資料" };
        }

        return new DiskIoResult(StatusAggregator.Aggregate(items.Select(i => i.Status)), items);
    }

    private static DiskIoItem Item(DiskIoScope scope, string name, DiskIoCounterSet set)
    {
        IReadOnlyList<PdhArrayValue> arrays = set.All;
        var values = new double[arrays.Count];
        bool notReady = false;
        bool anyFound = false;
        for (int i = 0; i < arrays.Count; i++)
        {
            PdhArrayValue array = arrays[i];
            if (array.Status == PdhReadStatus.Failed)
            {
                return Failed(scope, name, ErrorCode.DataError, ReadFailed(scope, i, array.ErrorCode));
            }

            if (array.Status == PdhReadStatus.NotReady || !array.Instances.TryGetValue(name, out PdhValue value))
            {
                notReady = true;
                continue;
            }

            anyFound = true;
            switch (value.Status)
            {
                case PdhReadStatus.Failed:
                    return Failed(scope, name, ErrorCode.DataError, ReadFailed(scope, i, value.ErrorCode));
                case PdhReadStatus.NotReady:
                    notReady = true;
                    break;
                default:
                    if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0)
                    {
                        return Failed(scope, name, ErrorCode.DataError, string.Create(
                            CultureInfo.InvariantCulture, $"{DiskIoCounterSet.CounterNames[i]} 數值不合理：{value.Value}"));
                    }

                    values[i] = value.Value;
                    break;
            }
        }

        bool anyArrayReady = arrays.Any(a => a.Status == PdhReadStatus.Ok);
        if (!anyFound && anyArrayReady)
        {
            return new DiskIoItem(MonitorStatus.Unavailable, scope, name, null, null, null, null, null, null, null)
            {
                ErrorCode = ErrorCode.Unavailable,
                ErrorMessage = "找不到該磁碟的計數器資料",
            };
        }

        if (notReady)
        {
            return new DiskIoItem(MonitorStatus.WarmingUp, scope, name, null, null, null, null, null, null, null);
        }

        return new DiskIoItem(
            MonitorStatus.OK,
            scope,
            name,
            values[0] / BytesPerMB,
            values[1] / BytesPerMB,
            values[2],
            values[3],
            values[4] * 1000,
            values[5] * 1000,
            Math.Clamp(100 - values[6], 0, 100));
    }

    private static string ReadFailed(DiskIoScope scope, int counter, uint code) =>
        string.Create(CultureInfo.InvariantCulture, $"讀取 {CounterPath(scope, DiskIoCounterSet.CounterNames[counter])} 失敗（PDH 0x{code:X8}）");

    private static DiskIoItem Failed(DiskIoScope scope, string name, ErrorCode code, string message) =>
        new(MonitorStatus.Error, scope, name, null, null, null, null, null, null, null) { ErrorCode = code, ErrorMessage = message };

    /// <summary>實體磁碟名稱開頭的編號（<c>0 C: D:</c> → 0）；沒有編號時排在最後。</summary>
    private static int PhysicalIndex(string name)
    {
        int end = name.IndexOf(' ', StringComparison.Ordinal);
        string head = end < 0 ? name : name[..end];
        return int.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ? index : int.MaxValue;
    }

    [GeneratedRegex("^[A-Za-z]:$")]
    private static partial Regex DriveLetter();

    private DiskIoResult Sample()
    {
        try
        {
            IPdhQuery query = EnsureQuery();
            query.Collect();
            var logical = DiskIoCounterSet.From([.. _logical!.Select(c => c.ReadArray())]);
            var physical = DiskIoCounterSet.From([.. _physical!.Select(c => c.ReadArray())]);
            return Calculate(FixedDrives(), logical, physical);
        }
        catch (PdhException ex)
        {
            Reset();
            return new DiskIoResult(MonitorStatus.Error, []) { ErrorCode = ErrorCode.TransientError, ErrorMessage = ex.Message };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Reset();
            return new DiskIoResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "無法載入 pdh.dll（需要 Windows）" };
        }
    }

    private string[]? FixedDrives()
    {
        try
        {
            return [.. drives.GetFixedDrives().Select(d => d.Drive)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private IPdhQuery EnsureQuery()
    {
        if (_query is null)
        {
            IPdhQuery query = factory.Create();
            try
            {
                _logical = [.. DiskIoCounterSet.CounterNames.Select(n => query.AddCounterArray(CounterPath(DiskIoScope.Logical, n)))];
                _physical = [.. DiskIoCounterSet.CounterNames.Select(n => query.AddCounterArray(CounterPath(DiskIoScope.Physical, n)))];
            }
            catch
            {
                query.Dispose();
                throw;
            }

            _query = query;
        }

        return _query;
    }

    private void Reset()
    {
        _query?.Dispose();
        _query = null;
        _logical = null;
        _physical = null;
    }
}
