using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.Text;
using PerformanceMonitor.Presentation.Trends;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.Presentation.Tests;

public class TrendTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private static MonitorSnapshot At(double seconds, params ModuleResult[] results) =>
        Samples.Snapshot(results) with { TimestampUtc = Samples.Time.AddSeconds(seconds) };

    private static TrendSample Cpu(double seconds, double? value) =>
        new(Samples.Time.AddSeconds(seconds), new Dictionary<TrendGroup, IReadOnlyDictionary<string, double?>>
        {
            [TrendGroup.CpuMemory] = new Dictionary<string, double?> { ["CPU"] = value },
        });

    private static TrendChart CpuChart(params TrendSample[] samples) =>
        TrendChartBuilder.Build("CPU", "%", samples, TrendGroup.CpuMemory, Interval, 100, 100, "尚無資料");

    [Fact]
    public void ExtractsValuesAndNullsForProblems()
    {
        TrendSample s = TrendHistory.Extract(At(
            0,
            new CpuResult(MonitorStatus.OK, 25),
            new MemoryResult(MonitorStatus.Error, null, null, null, null),
            new NetworkResult(MonitorStatus.OK, [new NetworkAdapterItem(MonitorStatus.OK, "E", null, null, true, 1, 1, 10, 2)], 10, 2),
            new DiskIoResult(MonitorStatus.OK, [
                new DiskIoItem(MonitorStatus.OK, DiskIoScope.Logical, "C:", 0, 0, 0, 0, 0, 0, 12.5),
                new DiskIoItem(MonitorStatus.WarmingUp, DiskIoScope.Logical, "D:", null, null, null, null, null, null, null),
                new DiskIoItem(MonitorStatus.OK, DiskIoScope.Physical, "0 C:", 0, 0, 0, 0, 0, 0, 12.5),
            ]),
            new GpuResult(MonitorStatus.Error, [
                new GpuItem(MonitorStatus.OK, 0, "RTX", 35, 1, 2, 61),
                new GpuItem(MonitorStatus.Error, -1, "", null, null, null, null),
                new GpuItem(MonitorStatus.Error, -1, "", null, null, null, null),
            ])));

        Assert.Equal(25, s.Values[TrendGroup.CpuMemory]["CPU"]);
        Assert.Null(s.Values[TrendGroup.CpuMemory]["記憶體"]);
        Assert.Equal(10, s.Values[TrendGroup.Network]["下載"]);
        Assert.Equal(["C:", "D:"], s.Values[TrendGroup.DiskBusy].Keys);
        Assert.Null(s.Values[TrendGroup.DiskBusy]["D:"]);
        Assert.Equal(35, s.Values[TrendGroup.GpuUsage]["GPU 0"]);
        Assert.Equal(61, s.Values[TrendGroup.GpuTemperature]["GPU 0"]);
        Assert.Equal(2, s.Values[TrendGroup.GpuUsage].Count); // 重複名稱只取第一個
    }

    [Fact]
    public void MissingModulesGiveNullOrEmpty()
    {
        TrendSample s = TrendHistory.Extract(At(0));

        Assert.Null(s.Values[TrendGroup.CpuMemory]["CPU"]);
        Assert.Null(s.Values[TrendGroup.Network]["下載"]);
        Assert.Empty(s.Values[TrendGroup.DiskBusy]);
        Assert.Empty(s.Values[TrendGroup.GpuUsage]);
    }

    [Fact]
    public void HistoryKeepsOnlyFiveMinutes()
    {
        var history = new TrendHistory();
        for (int i = 0; i <= 200; i++)
        {
            history.Add(At(i * 2, new CpuResult(MonitorStatus.OK, i)));
        }

        IReadOnlyList<TrendSample> samples = history.Samples;
        Assert.Equal(151, samples.Count); // 300 秒 ÷ 2 秒 + 1
        Assert.Equal(TimeSpan.FromMinutes(5), samples[^1].Time - samples[0].Time);
    }

    [Fact]
    public void HistorySubscribesToSnapshots()
    {
        var store = new Application.Monitoring.SnapshotStore();
        using var history = new TrendHistory(store);

        store.Publish(At(0, new CpuResult(MonitorStatus.OK, 5)));

        Assert.Equal(5, Assert.Single(history.Samples).Values[TrendGroup.CpuMemory]["CPU"]);
    }

    [Fact]
    public void NormalizesTimeAndValue()
    {
        // 取樣週期 150 秒，三點連成一條線
        TrendChart chart = TrendChartBuilder.Build(
            "CPU", "%", [Cpu(0, 0), Cpu(150, 50), Cpu(300, 100)], TrendGroup.CpuMemory, TimeSpan.FromSeconds(150), 100, 100, "尚無資料");

        ChartSeries series = Assert.Single(chart.Series);
        Assert.Equal([new ChartPoint(0, 0), new ChartPoint(0.5, 0.5), new ChartPoint(1, 1)], Assert.Single(series.Segments));
        Assert.Equal("100%", series.CurrentText);
        Assert.Equal("100%", chart.YMaxText);
        Assert.True(chart.HasData);
    }

    [Fact]
    public void LatestSampleIsAtRightEdge()
    {
        TrendChart chart = CpuChart(Cpu(0, 10), Cpu(2, 20));

        IReadOnlyList<ChartPoint> points = Assert.Single(Assert.Single(chart.Series).Segments);
        Assert.Equal(1, points[^1].X);
        Assert.Equal(1 - (2.0 / 300), points[0].X, 9);
    }

    [Fact]
    public void BreaksLineAtNullValues()
    {
        TrendChart chart = CpuChart(Cpu(0, 10), Cpu(2, 20), Cpu(4, null), Cpu(6, 30));

        ChartSeries series = Assert.Single(chart.Series);
        Assert.Equal([2, 1], series.Segments.Select(s => s.Count));
    }

    [Fact]
    public void BreaksLineAcrossStoppedPeriod()
    {
        // 間隔 2 秒；停止 60 秒後再開始 → 前後不連線
        TrendChart chart = CpuChart(Cpu(0, 10), Cpu(2, 20), Cpu(62, 30), Cpu(64, 40));

        Assert.Equal([2, 2], Assert.Single(chart.Series).Segments.Select(s => s.Count));
    }

    [Fact]
    public void SmallJitterDoesNotBreakLine()
    {
        TrendChart chart = CpuChart(Cpu(0, 10), Cpu(4.9, 20));

        Assert.Single(Assert.Single(chart.Series).Segments);
    }

    [Fact]
    public void LatestUnavailableShowsDash()
    {
        Assert.Equal("—", Assert.Single(CpuChart(Cpu(0, 10), Cpu(2, null)).Series).CurrentText);
    }

    [Fact]
    public void NoDataShowsEmptyText()
    {
        TrendChart none = CpuChart();
        TrendChart allNull = CpuChart(Cpu(0, null));

        Assert.False(none.HasData);
        Assert.Equal("尚無資料", none.EmptyText);
        Assert.False(allNull.HasData);
        Assert.Empty(allNull.Series);
    }

    [Fact]
    public void AutoScaleUsesNiceCeiling()
    {
        var samples = new[]
        {
            new TrendSample(Samples.Time, new Dictionary<TrendGroup, IReadOnlyDictionary<string, double?>>
            {
                [TrendGroup.Network] = new Dictionary<string, double?> { ["下載"] = 37.2, ["上傳"] = 1.234 },
            }),
        };

        TrendChart chart = TrendChartBuilder.Build("網路", "Mbps", samples, TrendGroup.Network, Interval, null, 1, "無");

        Assert.Equal(50, chart.YMax); // 37.2 × 1.1 = 40.9 → 50
        Assert.Equal("50 Mbps", chart.YMaxText);
        Assert.Equal(["上傳", "下載"], chart.Series.Select(s => s.Name));
        Assert.Equal([0, 1], chart.Series.Select(s => s.ColorIndex));
        Assert.Equal("1.23 Mbps", chart.Series[0].CurrentText);
        Assert.Equal("37.2 Mbps", chart.Series[1].CurrentText);
    }

    [Theory]
    [InlineData(0.3, 0.5)]
    [InlineData(1, 1)]
    [InlineData(1.01, 2)]
    [InlineData(3, 5)]
    [InlineData(7, 10)]
    [InlineData(120, 200)]
    [InlineData(0, 1)]
    public void NiceCeiling(double value, double expected) =>
        Assert.Equal(expected, TrendChartBuilder.NiceCeiling(value), 9);

    [Fact]
    public void ValuesAboveFixedMaxAreClamped()
    {
        ChartPoint p = Assert.Single(Assert.Single(Assert.Single(CpuChart(Cpu(0, 130)).Series).Segments));

        Assert.Equal(1, p.Y);
    }

    [Fact]
    public void ReferenceLinesAreNormalizedAndOutOfRangeDropped()
    {
        TrendChart chart = TrendChartBuilder.Build(
            "網路", "Mbps", [Cpu(0, 10)], TrendGroup.CpuMemory, Interval, null, 1, "無", [(15, "門檻"), (500, "太高")]);

        ChartReferenceLine line = Assert.Single(chart.ReferenceLines);
        Assert.Equal("門檻", line.Label);
        Assert.Equal(15 / chart.YMax, line.Y, 9);
    }

    [Fact]
    public void ThresholdLinesFollowAlertSettings()
    {
        var thresholds = new ThresholdSettings { CpuPercent = 90, RamPercent = 80 };

        Assert.Equal(
            [(90d, "CPU 告警 90%"), (80d, "記憶體告警 80%")],
            TrendViewModel.ThresholdLines(new AppSettings { Thresholds = thresholds }));
        Assert.Equal(
            [(90d, "CPU／記憶體告警 90%")],
            TrendViewModel.ThresholdLines(new AppSettings { Thresholds = thresholds with { RamPercent = 90 } }));
        Assert.Empty(TrendViewModel.ThresholdLines(new AppSettings { Alert = new AlertSettings { Enabled = false } }));
    }

    [Fact]
    public void TrendPageCpuChartHasThresholdLines()
    {
        var history = new TrendHistory();
        history.Add(At(0, new CpuResult(MonitorStatus.OK, 10)));
        var page = new TrendViewModel(history, new AppSettings());

        page.Refresh(new RefreshData(null, new Core.Models.Alerts.AlertView(true, [], [])));

        Assert.Single(page.Charts[0].ReferenceLines); // CPU 與記憶體預設門檻同為 90
        Assert.Empty(page.Charts[1].ReferenceLines);
    }

    [Fact]
    public void HoverFindsNearestPointPerSeries()
    {
        TrendChart chart = CpuChart(Cpu(0, 10), Cpu(2, 20), Cpu(4, 30));
        double x = chart.Series[0].Points[1].X;

        IReadOnlyList<HoverHit> hits = chart.HoverHits(x + 0.001);

        HoverHit hit = Assert.Single(hits);
        Assert.Equal(20, hit.Point.Value);
        string? text = chart.HoverText(x);
        Assert.NotNull(text);
        string[] lines = text.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Matches(@"^\d\d:\d\d:\d\d$", lines[0]);
        Assert.Equal("CPU　20.0%", lines[1]);
    }

    [Fact]
    public void HoverOutsideTolerancePointsReturnsNull()
    {
        // 間隔 2 秒；停止 60 秒：空檔中間沒有資料
        TrendChart chart = CpuChart(Cpu(0, 10), Cpu(2, 20), Cpu(62, 30), Cpu(64, 40));
        double gapX = (chart.Series[0].Points[1].X + chart.Series[0].Points[2].X) / 2;

        Assert.Empty(chart.HoverHits(gapX));
        Assert.Null(chart.HoverText(gapX));
    }

    [Fact]
    public void HoverTextSkipsSeriesWithoutNearbyPoint()
    {
        var samples = new[]
        {
            new TrendSample(Samples.Time, new Dictionary<TrendGroup, IReadOnlyDictionary<string, double?>>
            {
                [TrendGroup.CpuMemory] = new Dictionary<string, double?> { ["CPU"] = 10, ["記憶體"] = null },
            }),
        };
        TrendChart chart = TrendChartBuilder.Build("x", "%", samples, TrendGroup.CpuMemory, Interval, 100, 100, "無");

        string? text = chart.HoverText(1);

        Assert.NotNull(text);
        Assert.DoesNotContain("記憶體", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyChartHasNoHover()
    {
        Assert.Null(CpuChart().HoverText(0.5));
    }

    [Fact]
    public void SeriesFilterKeepsOnlyMatchingLines()
    {
        var sample = new TrendSample(Samples.Time, new Dictionary<TrendGroup, IReadOnlyDictionary<string, double?>>
        {
            [TrendGroup.CpuMemory] = new Dictionary<string, double?> { ["CPU"] = 10, ["記憶體"] = 60 },
        });

        TrendChart chart = TrendChartBuilder.Build(
            "x", "Mbps", [sample], TrendGroup.CpuMemory, Interval, null, 1, "無", null, name => name == "CPU");

        ChartSeries series = Assert.Single(chart.Series);
        Assert.Equal("CPU", series.Name);
        Assert.Equal(20, chart.YMax); // 只看 CPU：10 × 1.1 → 20，不受記憶體 60 影響
    }

    [Fact]
    public void OverviewSparkMapsModulesToTrendGroups()
    {
        var history = new TrendHistory();
        history.Add(At(
            0,
            new CpuResult(MonitorStatus.OK, 25),
            new MemoryResult(MonitorStatus.OK, 8, 16, 8, 50),
            new NetworkResult(MonitorStatus.OK, [new NetworkAdapterItem(MonitorStatus.OK, "E", null, null, true, 1, 1, 10, 2)], 10, 2)));
        IReadOnlyList<TrendSample> samples = history.Samples;

        TrendChart cpu = Assert.IsType<TrendChart>(OverviewViewModel.Spark(ModuleNames.Cpu, samples, Interval));
        TrendChart memory = Assert.IsType<TrendChart>(OverviewViewModel.Spark(ModuleNames.Memory, samples, Interval));
        TrendChart network = Assert.IsType<TrendChart>(OverviewViewModel.Spark(ModuleNames.Network, samples, Interval));

        Assert.Equal("CPU", Assert.Single(cpu.Series).Name);
        Assert.Equal("記憶體", Assert.Single(memory.Series).Name);
        Assert.Equal(["上傳", "下載"], network.Series.Select(x => x.Name));
        Assert.Null(OverviewViewModel.Spark(ModuleNames.Disk, samples, Interval));
        Assert.Null(OverviewViewModel.Spark(ModuleNames.Process, samples, Interval));
        Assert.Null(OverviewViewModel.Spark(ModuleNames.Gpu, samples, Interval)); // 沒有 GPU 資料
        Assert.Null(OverviewViewModel.Spark(ModuleNames.Cpu, [], Interval));
    }

    [Fact]
    public void OverviewCardsCarrySpark()
    {
        var history = new TrendHistory();
        history.Add(At(0, new CpuResult(MonitorStatus.OK, 25), new DiskResult(MonitorStatus.OK, [])));
        var page = new OverviewViewModel(history, new AppSettings());

        page.Refresh(new RefreshData(At(0, new CpuResult(MonitorStatus.OK, 25), new DiskResult(MonitorStatus.OK, [])), new Core.Models.Alerts.AlertView(true, [], [])));

        Assert.NotNull(page.Cards.Single(c => c.Title == UiText.ModuleTitle(ModuleNames.Cpu)).Spark);
        Assert.Null(page.Cards.Single(c => c.Title == UiText.ModuleTitle(ModuleNames.Disk)).Spark);
    }

    [Fact]
    public void TrendPageBuildsFiveCharts()
    {
        var history = new TrendHistory();
        history.Add(At(0, new CpuResult(MonitorStatus.OK, 10), ModuleResult.Unavailable(ModuleNames.Gpu, "找不到 nvidia-smi")));
        var page = new TrendViewModel(history, new AppSettings());

        page.Refresh(new RefreshData(null, new Core.Models.Alerts.AlertView(true, [], [])));

        Assert.Equal(["CPU 與記憶體使用率", "網路下載／上傳合計", "邏輯磁碟忙碌率", "GPU 使用率", "GPU 溫度"], page.Charts.Select(c => c.Title));
        Assert.True(page.Charts[0].HasData);
        Assert.Equal("無法使用（沒有 NVIDIA GPU 資料）", page.Charts[3].EmptyText);
        Assert.Equal("最近 5 分鐘；停止監控或無法取得的時段不畫線。", page.RangeText);
    }
}
