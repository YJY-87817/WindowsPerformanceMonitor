using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Html;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Tests;

public class SvgTrendChartTests
{
    private static readonly TrendChartDefinition Cpu = TrendChartDefinition.All[0];
    private static readonly string[] Sections = ["統計摘要", "趨勢", "尖峰時段", "告警事件", "高記憶體程序", "資料品質"];

    private static ReportModel Model(Action<ReportBuilder> fill)
    {
        var builder = new ReportBuilder(TestData.Period(1, 1), TestData.Taipei, 10);
        fill(builder);
        return builder.Build(TestData.Local(1, 12));
    }

    private static void CpuAt(ReportBuilder b, int hour, int minute, double value) =>
        b.AddMetric(new SeriesKey(ReportGroups.Cpu, "使用率", "%", SeriesRole.CpuUsage, "CPU<1>"), TestData.Local(1, hour, minute), value, MonitorStatus.OK);

    [Fact]
    public void BreaksLinesWhereThereIsNoData()
    {
        ReportModel model = Model(b =>
        {
            CpuAt(b, 9, 0, 10);
            CpuAt(b, 9, 1, 20);
            CpuAt(b, 9, 5, 30); // 中間 3 分鐘沒有資料
        });

        string html = SvgTrendChart.Render(Cpu, model.Series, model.Timeline);

        Assert.Equal(2, CountOf(html, "class=\"avg\""));
        Assert.Equal(2, CountOf(html, "class=\"max\""));
        Assert.Equal(3, CountOf(html, "<rect class=\"hit\""));
        Assert.Contains("<title>2026-11-01 09:05（每分鐘）\nCPU&lt;1&gt;：平均 30，最大 30</title>", html, StringComparison.Ordinal);
        Assert.Contains(">100</text>", html, StringComparison.Ordinal);
        Assert.Contains(">12:00</text>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("http", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyChartShowsReasonInsteadOfSvg()
    {
        ReportModel model = Model(_ => { });

        string html = SvgTrendChart.Render(TrendChartDefinition.All[4], model.Series, model.Timeline);

        Assert.DoesNotContain("<svg", html, StringComparison.Ordinal);
        Assert.Contains("沒有 NVIDIA GPU", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoScaleUsesNiceCeilingAndLegendUsesDevices()
    {
        ReportModel model = Model(b =>
        {
            b.AddMetric(new SeriesKey(ReportGroups.Network, "合計下載", "Mbps", SeriesRole.NetworkDownload, "下載"), TestData.Local(1, 9), 37, MonitorStatus.OK);
            b.AddMetric(new SeriesKey(ReportGroups.Network, "合計上傳", "Mbps", SeriesRole.NetworkUpload, "上傳"), TestData.Local(1, 9), 3, MonitorStatus.OK);
        });

        string html = SvgTrendChart.Render(TrendChartDefinition.All[3], model.Series, model.Timeline);

        Assert.Contains(">50</text>", html, StringComparison.Ordinal); // 37 × 1.1 → 50
        Assert.Contains("</i>下載</span>", html, StringComparison.Ordinal);
        Assert.Contains("</i>上傳</span>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.7, 1)]
    [InlineData(1, 1)]
    [InlineData(1.2, 2)]
    [InlineData(37, 50)]
    [InlineData(51, 100)]
    [InlineData(0, 1)]
    public void NiceCeiling(double value, double expected) => Assert.Equal(expected, SvgTrendChart.NiceCeiling(value));

    [Fact]
    public void FullReportHasAllSectionsInOrder()
    {
        string html = HtmlReportRenderer.Render(Model(b => CpuAt(b, 9, 0, 10)));

        int[] positions = [.. Sections.Select(h => html.IndexOf($"<h2>{h}</h2>", StringComparison.Ordinal))];
        Assert.All(positions, p => Assert.True(p > 0));
        Assert.Equal(positions.Order(), positions);
        Assert.Equal(6, CountOf(html, "<figure class=\"chart\">"));
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.Ordinal);
    }

    private static int CountOf(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
