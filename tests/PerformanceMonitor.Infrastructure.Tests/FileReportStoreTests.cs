using System.Text;
using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Infrastructure.Storage.Csv;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class FileReportStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("wpm-report-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FileReportStore Store() => new(new AppSettings(), new AppDataPaths(_root));

    [Fact]
    public void ReportCsvDefinitionsMatchWriterTables()
    {
        foreach (ReportCsvFile file in ReportCsvFile.All)
        {
            CsvTable table = Assert.Single(CsvTables.All, t => t.Name == file.Name);
            Assert.Equal(table.Version, file.Version);
            Assert.Equal(table.Columns, file.Columns);
            Assert.Equal(table.FileName(new DateOnly(2026, 11, 16)), file.FileName(new DateOnly(2026, 11, 16)));
        }
    }

    [Fact]
    public void ReadsDataAndLogsWhileFileIsOpenForWriting()
    {
        string data = Path.Combine(_root, "Data");
        Directory.CreateDirectory(data);
        string path = Path.Combine(data, "SystemMetrics_v1_20261116.csv");
        File.WriteAllText(path, "﻿a,b\r\n1,2\r\n", new UTF8Encoding(true));
        Directory.CreateDirectory(Path.Combine(_root, "Logs"));
        File.WriteAllText(Path.Combine(_root, "Logs", "Application_20261116.log"), "line\r\n");

        using var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        string[] lines = [.. Store().ReadLines(ReportFolder.Data, "SystemMetrics_v1_20261116.csv")!];

        Assert.Equal(["a,b", "1,2"], lines); // BOM 由讀取器移除
        Assert.Equal(["line"], Store().ReadLines(ReportFolder.Logs, "Application_20261116.log")!);
        Assert.Null(Store().ReadLines(ReportFolder.Data, "missing.csv"));
    }

    [Fact]
    public void SavesUtf8WithoutBomUnderReportsFolder()
    {
        FileReportStore store = Store();
        string target = Path.Combine(store.DefaultOutputDirectory, "Report_20261116-20261116.html");

        string saved = store.Save(target, "<p>中文</p>");
        store.Save(target, "<p>覆蓋</p>");

        Assert.Equal(Path.Combine(_root, "Reports", "Report_20261116-20261116.html"), saved);
        byte[] bytes = File.ReadAllBytes(saved);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("<p>覆蓋</p>", Encoding.UTF8.GetString(bytes));
    }
}
