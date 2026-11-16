using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.Application.Tests;

public class ReportServiceTests
{
    private static ReportPeriod Period => ReportPeriod.TryCreate(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 7), out _)!;

    [Fact]
    public void WritesHtmlToDefaultReportsFolder()
    {
        var store = new FakeReportStore();
        var service = new ReportService(new AppSettings(), store, new FakeClock(), TimeZoneInfo.Utc);

        ReportResult result = service.Generate(Period);

        Assert.Equal(Path.Combine("root", "Reports", "Report_20261101-20261107.html"), result.Path);
        Assert.Contains("<!DOCTYPE html>", store.Saved[result.Path], StringComparison.Ordinal);
        Assert.Equal(7, result.Model.Quality.DatesWithoutData.Count);
        Assert.Equal(new FakeClock().UtcNow, result.Model.GeneratedAt);
    }

    [Fact]
    public void UsesOutputPathAndCsvIntervalFromSettings()
    {
        var store = new FakeReportStore();
        var settings = new AppSettings { Monitor = new MonitorSettings { LogSeconds = 30 } };

        ReportResult result = new ReportService(settings, store, new FakeClock(), TimeZoneInfo.Utc).Generate(Period, "custom.html");

        Assert.Equal("custom.html", result.Path);
        Assert.Equal(30, result.Model.Quality.CsvIntervalSeconds);
    }
}
