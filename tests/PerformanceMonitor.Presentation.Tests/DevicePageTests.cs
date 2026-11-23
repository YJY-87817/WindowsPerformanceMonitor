using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Alerts;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Presentation.Text;
using PerformanceMonitor.Presentation.Trends;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.Presentation.Tests;

public class DevicePageTests
{
    private static RefreshData Data(params ModuleResult[] results) => new(Samples.Snapshot(results), new AlertView(true, [], []));

    [Fact]
    public void DiskPageShowsCapacityAndIo()
    {
        var page = new DiskViewModel(new TrendHistory(), new AppSettings());

        page.Refresh(Data(
            new DiskResult(MonitorStatus.Error, [
                new DiskItem(MonitorStatus.OK, "C:", "System", 200, 150, 50, 75, 25),
                new DiskItem(MonitorStatus.Error, "D:", null, null, null, null, null, null) { ErrorMessage = "容量為 0" },
            ]),
            new DiskIoResult(MonitorStatus.OK, [
                new DiskIoItem(MonitorStatus.OK, DiskIoScope.Logical, "C:", 1.234, 0.5, 120, 80, 2, 4.5, 25),
                new DiskIoItem(MonitorStatus.WarmingUp, DiskIoScope.Physical, "0 C: D:", null, null, null, null, null, null, null),
            ])));

        Assert.Equal(new DiskCapacityRow("C:", "System", "200.00 GB", "150.00 GB", "50.00 GB", "75.0%", "正常（OK）", StatusKind.Ok, null) { UsedRatio = 0.75, MarkerRatio = 0.9 }, page.Capacity[0]);
        Assert.Equal(new DiskCapacityRow("D:", string.Empty, "—", "—", "—", "—", "錯誤（Error）", StatusKind.Error, "容量為 0") { MarkerRatio = 0.9 }, page.Capacity[1]);
        Assert.Equal(new DiskIoRow("邏輯", "C:", "1.23 MB/s", "0.50 MB/s", "120 / 80", "2.00 / 4.50 ms", "25.0%", "正常（OK）", StatusKind.Ok, null), page.Io[0]);
        Assert.Equal(new DiskIoRow("實體", "0 C: D:", "— MB/s", "— MB/s", "— / —", "— / — ms", "—", "暖機中（WarmingUp）", StatusKind.Pending, null), page.Io[1]);
        Assert.Equal("磁碟容量：錯誤（Error）；磁碟 I/O：正常（OK）", page.ModuleStatus);
        Assert.Equal(StatusKind.Error, page.ModuleKind);
    }

    [Fact]
    public void PagesKeepLastDataBeforeFirstSample()
    {
        var page = new DiskViewModel(new TrendHistory(), new AppSettings());

        page.Refresh(new RefreshData(null, new AlertView(true, [], [])));

        Assert.Equal("尚未取樣", page.ModuleStatus);
        Assert.Empty(page.Capacity);
    }

    [Fact]
    public void NetworkPageShowsAdaptersAndTotals()
    {
        var page = new NetworkViewModel(new TrendHistory(), new AppSettings());

        page.Refresh(Data(new NetworkResult(MonitorStatus.OK, [
            new NetworkAdapterItem(MonitorStatus.OK, "Ethernet", "Intel", "{1}", true, 1, 1, 12.345, 1.5),
            new NetworkAdapterItem(MonitorStatus.Unavailable, "Wi-Fi", null, null, null, null, null, null, null) { ErrorMessage = "not connected" },
        ], 12.345, 1.5)));

        Assert.Equal(new NetworkRow("Ethernet", "Intel", "實體", "12.35 Mbps", "1.50 Mbps", "正常（OK）", StatusKind.Ok, null), page.Adapters[0]);
        Assert.Equal(new NetworkRow("Wi-Fi", string.Empty, "—", "— Mbps", "— Mbps", "無法使用（Unavailable）", StatusKind.Inactive, "not connected"), page.Adapters[1]);
        Assert.Equal("下載 12.35 Mbps／上傳 1.50 Mbps（1 / 2 張介面卡）", page.TotalText);
    }

    [Fact]
    public void NetworkWithoutAdaptersShowsReason()
    {
        var page = new NetworkViewModel(new TrendHistory(), new AppSettings());

        page.Refresh(Data(new NetworkResult(MonitorStatus.Unavailable, [], null, null) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "沒有符合條件的網路介面卡" }));

        Assert.Empty(page.Adapters);
        Assert.Equal("—", page.TotalText);
        Assert.Equal("網路：無法使用（Unavailable）（沒有符合條件的網路介面卡）", page.ModuleStatus);
    }

    [Fact]
    public void TemperaturePageListsGpuThenThermalZonesWithNote()
    {
        var page = new TemperatureViewModel(new TrendHistory(), new AppSettings());

        page.Refresh(Data(
            new GpuResult(MonitorStatus.OK, [new GpuItem(MonitorStatus.OK, 0, "RTX 4060", 35, 1, 2, 61)]),
            new TemperatureResult(MonitorStatus.OK, [new ThermalZoneItem(MonitorStatus.OK, @"\_TZ.THM0", 27.95), new ThermalZoneItem(MonitorStatus.Error, "TZ1", null)])));

        Assert.Equal(
            [
                new TemperatureRow("GPU", "GPU 0 RTX 4060", "61.0 °C", "正常（OK）", StatusKind.Ok, null),
                new TemperatureRow("ACPI 熱區", @"\_TZ.THM0", "28.0 °C", "正常（OK）", StatusKind.Ok, null),
                new TemperatureRow("ACPI 熱區", "TZ1", "—", "錯誤（Error）", StatusKind.Error, null),
            ],
            page.Sensors);
        Assert.Contains("不是 CPU 溫度", page.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessPageShowsTopNInLocalTime()
    {
        var page = new ProcessViewModel();

        page.Refresh(Data(new ProcessResult(MonitorStatus.OK, [
            new ProcessItem(1, "chrome", 1234, 1200.55, Samples.Time),
            new ProcessItem(2, "System", 4, 0.1, null),
        ], 3, 1)));

        Assert.Equal(new ProcessRow("1", "chrome", "1234", "1200.6 MB", "2026-11-13 14:00:00") { BarRatio = 1 }, page.Processes[0]);
        Assert.Equal("—", page.Processes[1].StartTime);
        Assert.Equal("略過 3 個程序（已結束或無法讀取）；1 個程序的啟動時間無權讀取", page.CountsText);
        Assert.Equal("程序（RAM 前幾名）：正常（OK）", page.ModuleStatus);
    }

    [Fact]
    public void DevicePagesBuildTrendCharts()
    {
        var history = new TrendHistory();
        history.Add(Samples.Snapshot(
            new DiskIoResult(MonitorStatus.OK, [new DiskIoItem(MonitorStatus.OK, DiskIoScope.Logical, "C:", 1.5, 0.5, 120, 80, 2, 4.5, 25)]),
            new NetworkResult(MonitorStatus.OK, [new NetworkAdapterItem(MonitorStatus.OK, "Ethernet", null, null, true, 1, 1, 10, 2)], 10, 2),
            new GpuResult(MonitorStatus.OK, [new GpuItem(MonitorStatus.OK, 0, "RTX", 35, 1, 2, 61)]),
            new TemperatureResult(MonitorStatus.OK, [new ThermalZoneItem(MonitorStatus.OK, "TZ00", 48.5), new ThermalZoneItem(MonitorStatus.Error, "TZ01", null)])));
        var settings = new AppSettings();
        RefreshData data = Data(
            new DiskIoResult(MonitorStatus.OK, []), new NetworkResult(MonitorStatus.OK, [], 0, 0), new GpuResult(MonitorStatus.OK, []));

        var disk = new DiskViewModel(history, settings);
        var network = new NetworkViewModel(history, settings);
        var temperature = new TemperatureViewModel(history, settings);
        disk.Refresh(data);
        network.Refresh(data);
        temperature.Refresh(data);

        Assert.Equal(["各磁碟讀取速度", "各磁碟寫入速度", "各磁碟忙碌率"], disk.Charts.Select(c => c.Title));
        Assert.All(disk.Charts, c => Assert.True(c.HasData));
        Assert.Equal("1.50 MB/s", disk.Charts[0].Series[0].CurrentText);
        Assert.Equal(["各介面卡下載", "各介面卡上傳"], network.Charts.Select(c => c.Title));
        Assert.Equal("10.0 Mbps", network.Charts[0].Series[0].CurrentText);
        TrendChart temp = Assert.Single(temperature.Charts);
        Assert.Equal(["GPU 0", "TZ00", "TZ01"], temp.Series.Select(x => x.Name));
        Assert.Equal("—", temp.Series[2].CurrentText); // 非 OK 的熱區不畫線
        Assert.Equal("48.5 °C", temp.Series[1].CurrentText);
    }

    [Fact]
    public void DevicePageChartsShowReasonWithoutData()
    {
        var disk = new DiskViewModel(new TrendHistory(), new AppSettings());
        var temperature = new TemperatureViewModel(new TrendHistory(), new AppSettings());

        disk.Refresh(Data());
        temperature.Refresh(Data());

        Assert.All(disk.Charts, c => Assert.False(c.HasData));
        Assert.Equal("尚無資料（沒有邏輯磁碟 I/O 資料）", disk.Charts[0].EmptyText);
        Assert.Equal("無法使用（沒有溫度資料）", Assert.Single(temperature.Charts).EmptyText);
    }

    [Fact]
    public void ProcessPageHasNoTrendCharts() =>
        Assert.Empty(new ProcessViewModel().Charts);

    [Theory]
    [InlineData(95, 5, true)]   // 可用 5% ≤ 門檻 10%
    [InlineData(90, 10, true)]  // 剛好等於門檻
    [InlineData(80, 20, false)]
    public void DiskBarMarksThresholdBreach(double usedPercent, double freePercent, bool breach)
    {
        var page = new DiskViewModel(new TrendHistory(), new AppSettings());

        page.Refresh(Data(new DiskResult(MonitorStatus.OK, [new DiskItem(MonitorStatus.OK, "C:", null, 100, usedPercent, freePercent, usedPercent, freePercent)])));

        DiskCapacityRow row = Assert.Single(page.Capacity);
        Assert.Equal(usedPercent / 100, row.UsedRatio);
        Assert.Equal(0.9, row.MarkerRatio, 9);
        Assert.Equal(breach, row.OverThreshold);
    }

    [Fact]
    public void DiskBarFollowsGbCondition()
    {
        // 可用 40%（百分比沒達門檻），但可用 4 GB ≤ GB 門檻 5 → any 模式視為達告警
        var settings = new AppSettings { Thresholds = new ThresholdSettings { DiskFreeGB = 5, DiskRecoveryFreeGB = 6 } };
        var page = new DiskViewModel(new TrendHistory(), settings);

        page.Refresh(Data(new DiskResult(MonitorStatus.OK, [new DiskItem(MonitorStatus.OK, "C:", null, 10, 6, 4, 60, 40)])));

        Assert.True(Assert.Single(page.Capacity).OverThreshold);
    }

    [Fact]
    public void DiskBarCustomThresholdMovesMarker()
    {
        var settings = new AppSettings { Thresholds = new ThresholdSettings { DiskFreePercent = 20, DiskRecoveryFreePercent = 25 } };
        var page = new DiskViewModel(new TrendHistory(), settings);

        page.Refresh(Data(new DiskResult(MonitorStatus.OK, [new DiskItem(MonitorStatus.OK, "C:", null, 100, 50, 50, 50, 50)])));

        Assert.Equal(0.8, Assert.Single(page.Capacity).MarkerRatio, 9);
    }

    [Fact]
    public void ProcessBarsAreRelativeToLargest()
    {
        var page = new ProcessViewModel();

        page.Refresh(Data(new ProcessResult(MonitorStatus.OK, [
            new ProcessItem(1, "a", 1, 800, null),
            new ProcessItem(2, "b", 2, 200, null),
            new ProcessItem(3, "c", 3, 0, null),
        ], 0, 0)));

        Assert.Equal([1, 0.25, 0], page.Processes.Select(p => p.BarRatio));
    }

    [Fact]
    public void ProcessBarsAreZeroWhenAllZero()
    {
        var page = new ProcessViewModel();

        page.Refresh(Data(new ProcessResult(MonitorStatus.OK, [new ProcessItem(1, "a", 1, 0, null)], 0, 0)));

        Assert.Equal(0, Assert.Single(page.Processes).BarRatio);
    }
}
