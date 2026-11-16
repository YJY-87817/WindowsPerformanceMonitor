using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Reporting.Statistics;

/// <summary>
/// 依程序名稱彙總 ProcessMetrics（決策 0008）：同名程序合併、不分 PID；同一次紀錄（相同 SampleId，連續寫入）的同名程序 Working Set 相加。
/// 只保留每個名稱的次數、總和與最大，記憶體與程序名稱數成正比。
/// </summary>
public sealed class ProcessAccumulator
{
    private readonly Dictionary<string, (int Count, double Sum, double Max)> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _current = new(StringComparer.OrdinalIgnoreCase);
    private string? _sampleId;
    private int _records;

    /// <summary>加入一列（Top N 中的一個程序）。</summary>
    public void Add(string sampleId, string name, double workingSetMB)
    {
        if (!string.Equals(sampleId, _sampleId, StringComparison.Ordinal))
        {
            Flush();
            _sampleId = sampleId;
            _records++;
        }

        _current[name] = _current.GetValueOrDefault(name) + workingSetMB;
    }

    /// <summary>產生統計。</summary>
    public ProcessReport ToReport()
    {
        Flush();
        _sampleId = null;
        ProcessSummary[] top =
        [
            .. _names.Select(n => new ProcessSummary(n.Key, n.Value.Count, _records == 0 ? 0 : (double)n.Value.Count / _records, n.Value.Sum / n.Value.Count, n.Value.Max))
                .OrderByDescending(p => p.MaxMB)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Take(ProcessReport.MaxCount),
        ];
        return new ProcessReport(_records, top);
    }

    private void Flush()
    {
        foreach ((string name, double mb) in _current)
        {
            (int count, double sum, double max) = _names.GetValueOrDefault(name, (0, 0, double.MinValue));
            _names[name] = (count + 1, sum + mb, Math.Max(max, mb));
        }

        _current.Clear();
    }
}
