using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Infrastructure.Storage.Csv;

namespace PerformanceMonitor.Infrastructure.Tests;

public class CsvTablesTests
{
    private static readonly SampleId Id = new(Guid.Parse("0123456789abcdef0123456789abcdef"));
    private static readonly DateTimeOffset Utc = new(2026, 11, 13, 16, 30, 0, TimeSpan.Zero);

    internal static MonitorSnapshot Snapshot(params ModuleResult[] results) =>
        new(Id, Utc, TimeSpan.FromHours(8), "PC,01", results, TimeSpan.FromMilliseconds(345.6789), LoopOverrun: false);

    private static string[][] Rows(CsvTable table, MonitorSnapshot snapshot) => [.. table.BuildRows(snapshot)];

    internal static MonitorSnapshot FullSnapshot() => Snapshot(
        new CpuResult(MonitorStatus.OK, 25.55555),
        new MemoryResult(MonitorStatus.OK, 16, 10.4321, 5.5679, 65.2),
        new DiskResult(MonitorStatus.OK, [new DiskItem(MonitorStatus.OK, "C:", "System", 500, 300, 200, 60, 40)]),
        new DiskIoResult(MonitorStatus.WarmingUp, [new DiskIoItem(MonitorStatus.WarmingUp, "C:", null, null, null, null, null, null, null)]),
        new GpuResult(MonitorStatus.OK, [new GpuItem(MonitorStatus.OK, 0, "RTX 4060", 35, 2048, 8192, 61)]),
        new NetworkResult(MonitorStatus.OK, [new NetworkAdapterItem(MonitorStatus.OK, "Ethernet", "Intel(R) Ethernet", "{guid}", true, 1000, 2000, 12.5, 2.3)], 12.5, 2.3),
        new ProcessResult(MonitorStatus.OK, [new ProcessItem(1, "chrome", 1234, 1200.55, Utc.AddHours(-1)), new ProcessItem(2, "Code", 5678, 850.2, null)], 0, 1),
        new TemperatureResult(MonitorStatus.OK, [new ThermalZoneItem(MonitorStatus.OK, @"\_TZ.THM0", 27.85)]));

    [Fact]
    public void EveryRowHasTheSameNumberOfFieldsAsTheHeader()
    {
        MonitorSnapshot snapshot = FullSnapshot();

        foreach (CsvTable table in CsvTables.All)
        {
            string[][] rows = Rows(table, snapshot);
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.Equal(table.Columns.Count, r.Length));
        }
    }

    [Fact]
    public void FileNamesIncludeSchemaVersionAndDate()
    {
        Assert.Equal("SystemMetrics_v1_20261114.csv", CsvTables.SystemMetrics.FileName(new DateOnly(2026, 11, 14)));
        Assert.Equal(
            ["SystemMetrics", "DiskMetrics", "DiskIoMetrics", "GpuMetrics", "NetworkMetrics", "ProcessMetrics", "TemperatureMetrics"],
            CsvTables.All.Select(t => t.Name));
    }

    [Fact]
    public void SystemMetricsRow()
    {
        string[] row = Assert.Single(Rows(CsvTables.SystemMetrics, FullSnapshot()));

        Assert.Equal(
            ["0123456789abcdef0123456789abcdef", "2026-11-13T16:30:00.000Z", "2026-11-14T00:30:00.000+08:00", "\"PC,01\"",
             "25.556", "OK", "16", "10.432", "5.568", "65.2", "OK", "OK", "WarmingUp", "OK", "OK", "OK", "OK", "345.679", "False"],
            row);
    }

    [Fact]
    public void SystemMetricsLeavesMissingModulesEmpty()
    {
        string[] row = Assert.Single(Rows(CsvTables.SystemMetrics, Snapshot(ModuleResult.Disabled(ModuleNames.Gpu))));

        Assert.Equal(string.Empty, row[4]);
        Assert.Equal(string.Empty, row[5]);
        Assert.Equal("Disabled", row[13]);
    }

    [Fact]
    public void DeviceRowsIncludeStatusAndErrorCode()
    {
        var disk = new DiskResult(MonitorStatus.Error, [new DiskItem(MonitorStatus.Error, "D:", null, null, null, null, null, null) { ErrorCode = ErrorCode.DataError }]);

        string[] row = Assert.Single(Rows(CsvTables.DiskMetrics, Snapshot(disk)));

        Assert.Equal(["0123456789abcdef0123456789abcdef", "2026-11-13T16:30:00.000Z", "D:", "", "", "", "", "", "", "Error", "DataError"], row);
    }

    [Fact]
    public void ModuleWithoutItemsWritesAStatusRow()
    {
        ModuleResult gpu = ModuleResult.Unavailable(ModuleNames.Gpu, "nvidia-smi not found");

        string[] row = Assert.Single(Rows(CsvTables.GpuMetrics, Snapshot(gpu)));

        Assert.Equal(["0123456789abcdef0123456789abcdef", "2026-11-13T16:30:00.000Z", "", "", "", "", "", "Unavailable", "Unavailable"], row);
    }

    [Fact]
    public void ModuleNotInSnapshotWritesNothing()
    {
        Assert.Empty(Rows(CsvTables.NetworkMetrics, Snapshot()));
        Assert.Empty(Rows(CsvTables.ProcessMetrics, Snapshot()));
        Assert.Empty(Rows(CsvTables.TemperatureMetrics, Snapshot()));
    }

    [Fact]
    public void ProcessRows()
    {
        string[][] rows = Rows(CsvTables.ProcessMetrics, FullSnapshot());

        Assert.Equal(["0123456789abcdef0123456789abcdef", "2026-11-13T16:30:00.000Z", "1", "chrome", "1234", "1200.55", "2026-11-13T15:30:00.000Z", "OK"], rows[0]);
        Assert.Equal(string.Empty, rows[1][6]);
    }

    [Fact]
    public void ProcessModuleFailureWritesStatusRow()
    {
        string[] row = Assert.Single(Rows(CsvTables.ProcessMetrics, Snapshot(ModuleResult.Failed(ModuleNames.Process, ErrorCode.AccessDenied, "x"))));

        Assert.Equal("Error", row[^1]);
    }

    [Fact]
    public void TemperatureRowsCombineGpuAndThermalZones()
    {
        string[][] rows = Rows(CsvTables.TemperatureMetrics, FullSnapshot());

        Assert.Equal(2, rows.Length);
        Assert.Equal(["Gpu", "GPU 0 RTX 4060", "61", "OK", ""], rows[0][2..]);
        Assert.Equal(["AcpiThermalZone", @"\_TZ.THM0", "27.85", "OK", ""], rows[1][2..]);
    }

    [Fact]
    public void TemperatureWithoutThermalZonesWritesStatusRow()
    {
        var zones = new TemperatureResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable };

        string[] row = Assert.Single(Rows(CsvTables.TemperatureMetrics, Snapshot(zones)));

        Assert.Equal(["AcpiThermalZone", "", "", "Unavailable", "Unavailable"], row[2..]);
    }
}
