using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Html;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Tests;

public class HtmlReportRendererTests
{
    private static ReportModel Model(Action<ReportBuilder> fill)
    {
        var builder = new ReportBuilder(TestData.Period(1, 2), TestData.Taipei, 10);
        fill(builder);
        return builder.Build(TestData.Local(3, 9));
    }

    [Fact]
    public void IsSelfContainedWithoutScriptsOrExternalResources()
    {
        string html = HtmlReportRenderer.Render(Model(b => b.AddMetric(new SeriesKey(ReportGroups.Cpu, "使用率", "%"), TestData.Local(1, 9), 12.345, MonitorStatus.OK)));

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
        Assert.Contains("Content-Security-Policy", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-11-01 ～ 2026-11-02（2 天）", html, StringComparison.Ordinal);
        Assert.Contains("12.35", html, StringComparison.Ordinal);
        Assert.Contains("Nearest Rank", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EncodesTextFromDataAndFileNames()
    {
        const string evil = "<script>alert('x')</script>&\"";
        string html = HtmlReportRenderer.Render(Model(b =>
        {
            b.AddMetric(new SeriesKey(ReportGroups.Network, evil + " 下載", "Mbps"), TestData.Local(1, 9), 1, MonitorStatus.OK);
            b.AddFileIssue(new FileIssue(evil, FileIssueKind.BadRows, 3, evil));
            b.AddSample(TestData.Local(1, 9), evil);
        }));

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;&amp;&quot;", html, StringComparison.Ordinal);
        Assert.Contains("略過 3 列無法解析的資料", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyReportExplainsMissingData()
    {
        string html = HtmlReportRenderer.Render(Model(b =>
        {
            b.AddDateWithoutData(new DateOnly(2026, 11, 1));
            b.AddDateWithoutData(new DateOnly(2026, 11, 2));
        }));

        Assert.Contains("期間內沒有任何監控資料", html, StringComparison.Ordinal);
        Assert.Contains("無法計算（期間內沒有 Application Log）", html, StringComparison.Ordinal);
        Assert.Contains("2026-11-01 ～ 2026-11-02", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ThermalZoneIsNeverCalledCpuTemperature()
    {
        string html = HtmlReportRenderer.Render(Model(b => b.AddMetric(new SeriesKey(ReportGroups.ThermalZone, @"\_TZ.THM0", "°C"), TestData.Local(1, 9), 40, MonitorStatus.OK)));

        Assert.Contains("不是 CPU 核心溫度", html, StringComparison.Ordinal);
        Assert.DoesNotContain("CPU 溫度", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatsNumbersRatiosAndDateRanges()
    {
        Assert.Equal("1,234.57", HtmlReportRenderer.Number(1234.5678));
        Assert.Equal("0", HtmlReportRenderer.Number(0));
        Assert.Equal("—", HtmlReportRenderer.Number(null));
        Assert.Equal("12.3%", HtmlReportRenderer.Ratio(0.1234));
        Assert.Equal("—", HtmlReportRenderer.Ratio(null));
        Assert.Equal(
            "2026-11-01 ～ 2026-11-03、2026-11-05",
            HtmlReportRenderer.DateRanges([new(2026, 11, 5), new(2026, 11, 1), new(2026, 11, 2), new(2026, 11, 3)]));
    }
}
