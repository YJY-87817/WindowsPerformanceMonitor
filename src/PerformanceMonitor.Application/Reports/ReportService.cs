using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Reporting;
using PerformanceMonitor.Reporting.Html;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Application.Reports;

/// <summary>產生報告的結果。</summary>
/// <param name="Path">報告檔完整路徑。</param>
/// <param name="Model">報告內容。</param>
public sealed record ReportResult(string Path, ReportModel Model);

/// <summary>
/// 產生報告（使用案例）：讀取 CSV 與 Log → 統計 → 產生 HTML → 寫檔。
/// 預設輸出 <c>&lt;資料根目錄&gt;\Reports\Report_YYYYMMDD-YYYYMMDD.html</c>，同名檔覆蓋（2026-11-16 確認）。
/// </summary>
public sealed class ReportService(AppSettings settings, IReportStore store, IClock clock, TimeZoneInfo zone)
{
    /// <summary>預設檔名。</summary>
    public static string DefaultFileName(ReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return string.Create(CultureInfo.InvariantCulture, $"Report_{period.From:yyyyMMdd}-{period.To:yyyyMMdd}.html");
    }

    /// <summary>產生報告；<paramref name="outputPath"/> 為 null 時寫到預設位置。</summary>
    public ReportResult Generate(ReportPeriod period, string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(period);
        ReportModel model = Build(period);
        string path = outputPath ?? System.IO.Path.Combine(store.DefaultOutputDirectory, DefaultFileName(period));
        string saved = store.Save(path, HtmlReportRenderer.Render(model));
        return new ReportResult(saved, model);
    }

    /// <summary>只計算報告內容（不寫檔）。</summary>
    public ReportModel Build(ReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        var builder = new ReportBuilder(period, zone, settings.Monitor.LogSeconds);
        new ReportDataReader(store).Read(period, builder);
        return builder.Build(TimeZoneInfo.ConvertTime(clock.UtcNow, zone));
    }
}
