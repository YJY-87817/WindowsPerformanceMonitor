using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Reporting.Statistics;

/// <summary>各狀態的筆數。</summary>
public sealed class StatusCounts
{
    private readonly int[] _counts = new int[Enum.GetValues<MonitorStatus>().Length];

    /// <summary>總筆數。</summary>
    public int Total { get; private set; }

    /// <summary>指定狀態的筆數。</summary>
    public int this[MonitorStatus status] => _counts[(int)status];

    /// <summary>無法取得（<c>Unavailable</c>、<c>Error</c>）的比例；沒有資料時為 null。</summary>
    public double? UnavailableRatio => Total == 0 ? null : (double)(this[MonitorStatus.Unavailable] + this[MonitorStatus.Error]) / Total;

    /// <summary>加一筆。</summary>
    public void Add(MonitorStatus status)
    {
        _counts[(int)status]++;
        Total++;
    }
}
