using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting;

/// <summary>
/// 逐筆收集資料並產生 <see cref="ReportModel"/>（純邏輯，不讀檔）。
/// 呼叫順序：先加入所有 Log 監控事件（<see cref="AddSessionEvent"/>），再加入 CSV 紀錄；
/// 未記錄停止的監控時段以時段內最後一筆 SystemMetrics 紀錄為結束。
/// 指標依分組排序，同一分組內依第一次出現的順序（讀取端決定，例如「忙碌、讀取、寫入」）。
/// </summary>
public sealed class ReportBuilder
{
    private readonly ReportPeriod _period;
    private readonly int _csvIntervalSeconds;
    private readonly Dictionary<SeriesKey, SeriesAccumulator> _series = [];
    private readonly List<SeriesAccumulator> _seriesInOrder = [];
    private readonly Dictionary<string, StatusCounts> _modules = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _computers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(DateTimeOffset Time, SessionEventKind Kind)> _events = [];
    private readonly List<FileIssue> _fileIssues = [];
    private readonly List<DateOnly> _datesWithoutData = [];
    private List<PendingSpan>? _spans;
    private DateTimeOffset? _earliest;
    private DateTimeOffset? _latest;
    private int _samples;
    private bool _logsFound;

    /// <summary>建立。</summary>
    /// <param name="period">期間。</param>
    /// <param name="zone">本機時區（決定期間起訖與趨勢時段）。</param>
    /// <param name="csvIntervalSeconds">CSV 紀錄間隔（計算預期筆數）。</param>
    public ReportBuilder(ReportPeriod period, TimeZoneInfo zone, int csvIntervalSeconds)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentOutOfRangeException.ThrowIfLessThan(csvIntervalSeconds, 1);
        _period = period;
        _csvIntervalSeconds = csvIntervalSeconds;
        Timeline = new ReportTimeline(period, zone);
    }

    /// <summary>時間軸。</summary>
    public ReportTimeline Timeline { get; }

    /// <summary>期間內有 Application Log。</summary>
    public void MarkLogsFound() => _logsFound = true;

    /// <summary>加入一個 Log 監控事件。須在任何 <see cref="AddSample"/> 之前。</summary>
    public void AddSessionEvent(DateTimeOffset time, SessionEventKind kind)
    {
        if (_spans is not null)
        {
            throw new InvalidOperationException("監控事件須在 CSV 紀錄之前加入。");
        }

        _events.Add((time, kind));
    }

    /// <summary>加入一筆 SystemMetrics 紀錄（實際筆數、最早／最晚時間、電腦名稱）。</summary>
    public void AddSample(DateTimeOffset time, string? computerName)
    {
        _spans ??= Seal();
        _samples++;
        _earliest = _earliest is DateTimeOffset e && e <= time ? e : time;
        _latest = _latest is DateTimeOffset l && l >= time ? l : time;
        if (!string.IsNullOrWhiteSpace(computerName))
        {
            _computers.Add(computerName);
        }

        PendingSpan? span = SpanAt(time);
        if (span is { End: null })
        {
            span.LastSample = span.LastSample is DateTimeOffset s && s >= time ? s : time;
        }
    }

    /// <summary>加入一筆模組狀態（SystemMetrics 的狀態欄）。</summary>
    public void AddModuleStatus(string module, MonitorStatus status)
    {
        if (!_modules.TryGetValue(module, out StatusCounts? counts))
        {
            _modules[module] = counts = new StatusCounts();
        }

        counts.Add(status);
    }

    /// <summary>加入一個指標值。</summary>
    public void AddMetric(SeriesKey key, DateTimeOffset time, double? value, MonitorStatus status)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_series.TryGetValue(key, out SeriesAccumulator? series))
        {
            _series[key] = series = new SeriesAccumulator(key, Timeline);
            _seriesInOrder.Add(series);
        }

        series.Add(time, value, status);
    }

    /// <summary>加入檔案問題。</summary>
    public void AddFileIssue(FileIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        _fileIssues.Add(issue);
    }

    /// <summary>記錄完全沒有資料檔的日期。</summary>
    public void AddDateWithoutData(DateOnly date) => _datesWithoutData.Add(date);

    /// <summary>產生報告內容。</summary>
    public ReportModel Build(DateTimeOffset generatedAt)
    {
        _spans ??= Seal();
        MonitoringSpan[] sessions = [.. _spans.Select(ToSpan)];
        string[] moduleOrder = [ReportGroups.Cpu, ReportGroups.Memory, ReportGroups.Disk, ReportGroups.DiskIo, ReportGroups.Gpu, ReportGroups.Network, ReportGroups.Process, ReportGroups.ThermalZone];
        ModuleAvailability[] modules =
        [
            .. _modules.OrderBy(m => Array.IndexOf(moduleOrder, m.Key) is int i && i >= 0 ? i : int.MaxValue)
                .ThenBy(m => m.Key, StringComparer.Ordinal)
                .Select(m => new ModuleAvailability(m.Key, m.Value)),
        ];
        SeriesStatistics[] series =
        [
            .. _seriesInOrder.Select(s => s.ToStatistics())
                .OrderBy(s => ReportGroups.IndexOf(s.Key.Group))
                .ThenBy(s => s.Key.Group, StringComparer.Ordinal),
        ];

        var quality = new DataQuality(
            _earliest, _latest, _samples, _csvIntervalSeconds, _logsFound, sessions, modules, [.. _fileIssues], [.. _datesWithoutData]);
        return new ReportModel(_period, Timeline, generatedAt, [.. _computers], series, quality);
    }

    private MonitoringSpan ToSpan(PendingSpan span)
    {
        DateTimeOffset end = span.End ?? span.LastSample ?? span.Start;
        SessionEnd kind = span.End is null ? SessionEnd.NotRecorded : span.Kind;
        if (end < span.Start)
        {
            end = span.Start;
        }

        int expected = kind == SessionEnd.Once ? 1 : (int)Math.Floor((end - span.Start).TotalSeconds / _csvIntervalSeconds) + 1;
        return new MonitoringSpan(span.Start, end, kind, span.StartedBeforePeriod, expected);
    }

    private List<PendingSpan> Seal()
    {
        var spans = new List<PendingSpan>();
        PendingSpan? open = null;
        foreach ((DateTimeOffset time, SessionEventKind kind) in _events.OrderBy(e => e.Time))
        {
            switch (kind)
            {
                case SessionEventKind.Started or SessionEventKind.OnceStarted:
                    open = new PendingSpan(time, kind == SessionEventKind.OnceStarted ? SessionEnd.Once : SessionEnd.Stopped, false);
                    spans.Add(open);
                    break;
                default:
                    SessionEnd end = kind switch
                    {
                        SessionEventKind.Faulted => SessionEnd.Faulted,
                        SessionEventKind.OnceCompleted => SessionEnd.Once,
                        _ => SessionEnd.Stopped,
                    };
                    if (open is null)
                    {
                        // 期間之前就已開始（或開始那天的 Log 遺失）。
                        open = new PendingSpan(spans.Count == 0 ? Timeline.Start : time, end, spans.Count == 0);
                        spans.Add(open);
                    }

                    open.End = time;
                    open.Kind = end;
                    open = null;
                    break;
            }
        }

        return spans;
    }

    private PendingSpan? SpanAt(DateTimeOffset time)
    {
        int low = 0;
        int high = _spans!.Count - 1;
        PendingSpan? found = null;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (_spans[mid].Start <= time)
            {
                found = _spans[mid];
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return found;
    }

    private sealed class PendingSpan(DateTimeOffset start, SessionEnd kind, bool startedBeforePeriod)
    {
        public DateTimeOffset Start { get; } = start;

        public bool StartedBeforePeriod { get; } = startedBeforePeriod;

        public SessionEnd Kind { get; set; } = kind;

        public DateTimeOffset? End { get; set; }

        public DateTimeOffset? LastSample { get; set; }
    }
}
