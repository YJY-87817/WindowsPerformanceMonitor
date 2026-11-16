using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Reporting;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Application.Tests;

public class ReportDataReaderTests
{
    private static readonly TimeZoneInfo Taipei = TimeZoneInfo.CreateCustomTimeZone("Test+8", TimeSpan.FromHours(8), "Test+8", "Test+8");
    private static readonly DateOnly Day = new(2026, 11, 16);
    private readonly FakeReportStore _store = new();

    private static string Header(ReportCsvFile file) => "﻿" + string.Join(',', file.Columns);

    private static string Sys(string time, string cpu, string cpuStatus, string memory = "50", string gpuStatus = "Unavailable") =>
        $"id{time[17..19]},{time},2026-11-16T09:00:00.000+08:00,PC1,{cpu},{cpuStatus},16,{(memory.Length == 0 ? "" : "8")},8,{memory},OK,OK,OK,{gpuStatus},OK,OK,Unavailable,40,False";

    private void Add(ReportCsvFile file, params string[] rows) => _store.Data[file.FileName(Day)] = [Header(file), .. rows];

    private void Log(params string[] lines) => _store.Logs[ReportCsvFile.ApplicationLogName(Day)] = [.. lines];

    private ReportModel Read(DateOnly? from = null, DateOnly? to = null)
    {
        ReportPeriod period = ReportPeriod.TryCreate(from ?? Day, to ?? Day, out _)!;
        var builder = new ReportBuilder(period, Taipei, 10);
        new ReportDataReader(_store).Read(period, builder);
        return builder.Build(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.FromHours(8)));
    }

    private static SeriesStatistics Series(ReportModel model, string group, string name) =>
        Assert.Single(model.Series, s => s.Key.Group == group && s.Key.Name == name);

    private void AddFullDay()
    {
        Log(
            "﻿2026-11-16T08:59:59.000+08:00 [INFO] [Startup] 版本 0.1.0",
            "2026-11-16T09:00:00.000+08:00 [INFO] [Monitor] 監控開始",
            "2026-11-16T09:00:05.000+08:00 [WARN] [Alert] 告警 CPU：目前 95%，門檻 90%",
            "2026-11-16T09:00:20.000+08:00 [INFO] [Monitor] 監控正常停止；本次 CSV 寫入 3 列、遺失 0 列。");
        Add(
            ReportCsvFile.SystemMetrics,
            Sys("2026-11-16T01:00:00.000Z", "10", "OK"),
            Sys("2026-11-16T01:00:10.000Z", "30", "OK"),
            Sys("2026-11-16T01:00:20.000Z", "", "Error", memory: ""),
            "broken,row",
            Sys("2026-11-16 01:00:30", "5", "OK"));
        Add(
            ReportCsvFile.DiskMetrics,
            "id00,2026-11-16T01:00:00.000Z,C:,系統,100,80,20,80,20,OK,",
            "id10,2026-11-16T01:00:10.000Z,C:,系統,100,82,18,82,18,OK,");
        Add(
            ReportCsvFile.DiskIoMetrics,
            "id00,2026-11-16T01:00:00.000Z,Logical,C:,1,2,3,4,0.5,0.6,10,OK,",
            "id00,2026-11-16T01:00:00.000Z,Physical,0 C:,1,2,3,4,0.5,0.6,12,OK,",
            "id10,2026-11-16T01:00:10.000Z,Logical,C:,,,,,,,,WarmingUp,");
        Add(ReportCsvFile.GpuMetrics, "id00,2026-11-16T01:00:00.000Z,,,,,,Unavailable,Unavailable");
        Add(
            ReportCsvFile.NetworkMetrics,
            "id00,2026-11-16T01:00:00.000Z,Wi-Fi,\"Intel, Wireless\",{1},True,100,200,10,1,OK,",
            "id00,2026-11-16T01:00:00.000Z,Ethernet,Realtek,{2},True,100,200,5,2,OK,",
            "id10,2026-11-16T01:00:10.000Z,Wi-Fi,\"Intel, Wireless\",{1},True,100,200,20,3,OK,",
            "id10,2026-11-16T01:00:10.000Z,Ethernet,Realtek,{2},True,100,200,,,Error,DataError",
            "id20,2026-11-16T01:00:20.000Z,,,,,,,,,Unavailable,Unavailable");
        Add(
            ReportCsvFile.ProcessMetrics,
            "id00,2026-11-16T01:00:00.000Z,1,chrome,100,500,2026-11-16T00:00:00.000Z,OK",
            "id00,2026-11-16T01:00:00.000Z,2,chrome,101,300,2026-11-16T00:00:00.000Z,OK",
            "id00,2026-11-16T01:00:00.000Z,3,Code,200,600,2026-11-16T00:00:00.000Z,OK",
            "id10,2026-11-16T01:00:10.000Z,1,Code,200,900,2026-11-16T00:00:00.000Z,OK",
            "id20,2026-11-16T01:00:20.000Z,,,,,,Unavailable");
        Add(
            ReportCsvFile.TemperatureMetrics,
            "id00,2026-11-16T01:00:00.000Z,AcpiThermalZone,\\_TZ.THM0,45.5,OK,",
            "id00,2026-11-16T01:00:00.000Z,Gpu,GPU 0 RTX,60,OK,");
    }

    [Fact]
    public void ReadsAllFilesIntoStatistics()
    {
        AddFullDay();

        ReportModel model = Read();

        SeriesStatistics cpu = Series(model, ReportGroups.Cpu, "使用率");
        Assert.Equal(2, cpu.ValidCount);
        Assert.Equal(3, cpu.Statuses.Total);
        Assert.Equal(20, cpu.Average);
        Assert.Equal(30, cpu.Max);
        Assert.Equal(82, Series(model, ReportGroups.Disk, "C: 已用").Max);
        Assert.Equal(1, Series(model, ReportGroups.DiskIo, "C: 忙碌").ValidCount);
        Assert.Equal(2, Series(model, ReportGroups.DiskIo, "C: 忙碌").Statuses.Total);
        Assert.Equal(12, Series(model, ReportGroups.DiskIo, "實體磁碟 0 C: 忙碌").Max);
        Assert.Equal(45.5, Series(model, ReportGroups.ThermalZone, @"\_TZ.THM0").Average);
        Assert.Equal(60, Series(model, ReportGroups.GpuTemperature, "GPU 0 RTX").Average);
        Assert.DoesNotContain(model.Series, s => s.Key.Group == ReportGroups.Gpu); // 只有狀態列

        // 網路合計：每次取樣 OK 介面卡相加；全部無法取得時不是 0。
        SeriesStatistics download = Series(model, ReportGroups.Network, "合計下載");
        Assert.Equal(3, download.Statuses.Total);
        Assert.Equal(2, download.ValidCount);
        Assert.Equal(20, download.Max); // 15、20
        Assert.Equal(1, download.Statuses[MonitorStatus.Unavailable]);
        Assert.Equal(1, Series(model, ReportGroups.Network, "Ethernet 下載").Statuses[MonitorStatus.Error]);

        DataQuality q = model.Quality;
        Assert.Equal(3, q.ActualSamples);
        Assert.Equal(3, q.ExpectedSamples);
        Assert.Equal(0, q.MissingRatio);
        Assert.Equal(SessionEnd.Stopped, Assert.Single(q.Sessions).EndKind);
        Assert.Equal(["PC1"], model.ComputerNames);
        Assert.Equal(3, q.Modules.Single(m => m.Module == ReportGroups.Gpu).Statuses[MonitorStatus.Unavailable]);
        Assert.Equal(8, q.Modules.Count);

        // 程序：同一次紀錄的同名程序相加；狀態列不算紀錄。
        Assert.Equal(2, model.Processes.Records);
        Assert.Equal(["Code", "chrome"], model.Processes.Top.Select(p => p.Name));
        Assert.Equal(900, model.Processes.Top[0].MaxMB);
        Assert.Equal(750, model.Processes.Top[0].AverageMB);
        Assert.Equal(800, model.Processes.Top[1].MaxMB);
        Assert.Equal(0.5, model.Processes.Top[1].AppearanceRatio);

        // 告警：沒有恢復紀錄，算到該次監控結束（09:00:20）。
        AlertSummary alert = Assert.Single(model.Alerts.Summaries);
        Assert.Equal("CPU", alert.Target);
        Assert.Equal(1, alert.Warnings);
        Assert.Equal(TimeSpan.FromSeconds(15), alert.Duration);
        Assert.True(alert.Unrecovered);
        Assert.Equal("95%", alert.LastValueText);
        Assert.Equal("90%", Assert.Single(model.Alerts.Recent).Threshold);

        // 尖峰：CPU 兩筆在同一分鐘（同一段）。
        PeakList cpuPeaks = Assert.Single(model.Peaks, p => p.Title == "CPU 使用率");
        Assert.Equal(20, Assert.Single(cpuPeaks.Periods).Average);
        Assert.Equal("C:", Assert.Single(model.Peaks, p => p.ByDevice && p.Title.StartsWith("磁碟", StringComparison.Ordinal)).Periods[0].Device);

        FileIssue bad = Assert.Single(q.FileIssues);
        Assert.Equal(FileIssueKind.BadRows, bad.Kind);
        Assert.Equal(2, bad.Count);
        Assert.Equal("SystemMetrics_v1_20261116.csv", bad.FileName);
        Assert.Contains("第 5 行", bad.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void HeaderMismatchSkipsWholeFileAndEmptyFileIsReported()
    {
        AddFullDay();
        _store.Data[ReportCsvFile.DiskMetrics.FileName(Day)] = ["SampleId,TimestampUtc,Drive", "id00,2026-11-16T01:00:00.000Z,C:"];
        _store.Data[ReportCsvFile.GpuMetrics.FileName(Day)] = [];

        ReportModel model = Read();

        Assert.DoesNotContain(model.Series, s => s.Key.Group == ReportGroups.Disk);
        Assert.Contains(model.Quality.FileIssues, i => i.Kind == FileIssueKind.HeaderMismatch && i.FileName.StartsWith("DiskMetrics", StringComparison.Ordinal));
        Assert.Contains(model.Quality.FileIssues, i => i.Kind == FileIssueKind.HeaderMismatch && i.Detail == "空檔");
    }

    [Fact]
    public void MissingFilesAndDatesWithoutDataAreListed()
    {
        Add(ReportCsvFile.SystemMetrics, Sys("2026-11-16T01:00:00.000Z", "10", "OK"));

        ReportModel model = Read(Day.AddDays(-2), Day);

        Assert.Equal([Day.AddDays(-2), Day.AddDays(-1)], model.Quality.DatesWithoutData);
        Assert.Equal(7, model.Quality.FileIssues.Count(i => i.Kind == FileIssueKind.Missing)); // 6 個 CSV ＋ Application Log
        Assert.Contains(model.Quality.FileIssues, i => i.FileName == "Application_20261116.log" && i.Detail!.Contains("監控時段", StringComparison.Ordinal));
        Assert.False(model.Quality.LogsFound);
        Assert.Null(model.Quality.ExpectedSamples);
    }

    [Fact]
    public void UnreadableFileIsReportedAndOthersContinue()
    {
        AddFullDay();
        _store.Unreadable.Add(ReportCsvFile.DiskIoMetrics.FileName(Day));

        ReportModel model = Read();

        FileIssue issue = Assert.Single(model.Quality.FileIssues, i => i.Kind == FileIssueKind.Unreadable);
        Assert.Equal("DiskIoMetrics_v1_20261116.csv", issue.FileName);
        Assert.DoesNotContain(model.Series, s => s.Key.Group == ReportGroups.DiskIo);
        Assert.Contains(model.Series, s => s.Key.Group == ReportGroups.Network);
    }

    [Fact]
    public void InvalidStatusOrScopeMakesRowBad()
    {
        Add(
            ReportCsvFile.DiskIoMetrics,
            "id00,2026-11-16T01:00:00.000Z,Remote,C:,1,2,3,4,0.5,0.6,10,OK,",
            "id00,2026-11-16T01:00:00.000Z,Logical,C:,1,2,3,4,0.5,0.6,10,Good,");
        Add(ReportCsvFile.TemperatureMetrics, "id00,2026-11-16T01:00:00.000Z,Cpu,Core 0,50,OK,");

        ReportModel model = Read();

        Assert.Empty(model.Series);
        Assert.Equal(2, model.Quality.FileIssues.Single(i => i.FileName.StartsWith("DiskIo", StringComparison.Ordinal)).Count);
        Assert.Equal(1, model.Quality.FileIssues.Single(i => i.Kind == FileIssueKind.BadRows && i.FileName.StartsWith("Temperature", StringComparison.Ordinal)).Count);
    }

    [Theory]
    [InlineData("告警 Disk C:：目前 可用 5%（10 GB），門檻 可用 < 10%", AlertLogKind.Warning, "Disk C:", "可用 5%（10 GB）", "可用 < 10%")]
    [InlineData("告警持續（冷卻結束後再次通知） RAM：目前 92%，門檻 90%", AlertLogKind.Repeat, "RAM", "92%", "90%")]
    [InlineData("已恢復 CPU：目前 40%，門檻 85%", AlertLogKind.Recovered, "CPU", "40%", "85%")]
    public void ParsesAlertMessages(string message, AlertLogKind kind, string target, string value, string threshold)
    {
        AlertLogEvent? e = ReportDataReader.ParseAlert(new LogLine(DateTimeOffset.UnixEpoch, "WARN", "Alert", message));

        Assert.NotNull(e);
        Assert.Equal(kind, e.Kind);
        Assert.Equal(target, e.Target);
        Assert.Equal(value, e.ValueText);
        Assert.Equal(threshold, e.Threshold);
    }

    [Theory]
    [InlineData("Alert", "其他訊息")]
    [InlineData("Alert", "告警 CPU 沒有冒號")]
    [InlineData("Monitor", "告警 CPU：目前 95%，門檻 90%")]
    public void IgnoresOtherMessages(string source, string message) =>
        Assert.Null(ReportDataReader.ParseAlert(new LogLine(DateTimeOffset.UnixEpoch, "INFO", source, message)));
}
