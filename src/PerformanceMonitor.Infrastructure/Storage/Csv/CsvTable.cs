using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Infrastructure.Storage.Csv;

/// <summary>
/// 一種 CSV 檔：名稱、schema 版本、欄位與由快照產生資料列的方式。
/// 檔名為 <c>&lt;Name&gt;_v&lt;Version&gt;_&lt;本機日期 YYYYMMDD&gt;.csv</c>（決策 0005）。
/// </summary>
/// <param name="Name">類型名稱，例如 <c>SystemMetrics</c>。</param>
/// <param name="Version">schema 版本；欄位改變時 +1。</param>
/// <param name="Columns">欄位名稱。</param>
/// <param name="BuildRows">由快照產生資料列；每列欄位數須等於 <paramref name="Columns"/>，文字已跳脫。</param>
public sealed record CsvTable(string Name, int Version, IReadOnlyList<string> Columns, Func<MonitorSnapshot, IEnumerable<string[]>> BuildRows)
{
    /// <summary>指定日期的檔名。</summary>
    public string FileName(DateOnly localDate) => $"{Name}_v{Version}_{localDate:yyyyMMdd}.csv";

    /// <summary>標題列。</summary>
    public string Header => CsvFormat.Line(Columns);
}
