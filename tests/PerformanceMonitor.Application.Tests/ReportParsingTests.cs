using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Application.Tests;

public class ReportParsingTests
{
    [Theory]
    [InlineData("a,b,c", new[] { "a", "b", "c" })]
    [InlineData("a,,", new[] { "a", "", "" })]
    [InlineData("", new[] { "" })]
    [InlineData("\"x,y\",z", new[] { "x,y", "z" })]
    [InlineData("\"say \"\"hi\"\"\",1", new[] { "say \"hi\"", "1" })]
    [InlineData("\"\",2", new[] { "", "2" })]
    public void SplitsCsv(string line, string[] expected) => Assert.Equal(expected, CsvLineParser.Split(line));

    [Theory]
    [InlineData("\"unterminated,1")]
    [InlineData("\"x\"y,1")]
    [InlineData("a\"b,1")]
    public void MalformedCsvIsNull(string line) => Assert.Null(CsvLineParser.Split(line));

    [Fact]
    public void ParsesLogLines()
    {
        LogLine? line = LogLineParser.Parse("﻿2026-11-16T09:00:01.250+08:00 [INFO] [Monitor] 監控開始");

        Assert.NotNull(line);
        Assert.Equal(new DateTimeOffset(2026, 11, 16, 9, 0, 1, 250, TimeSpan.FromHours(8)), line.Time);
        Assert.Equal("INFO", line.Level);
        Assert.Equal("Monitor", line.Source);
        Assert.Equal("監控開始", line.Message);
        Assert.Equal("告警 CPU：目前 95%，門檻 90%", LogLineParser.Parse("2026-11-16T09:00:01.250+08:00 [WARN] [Alert] 告警 CPU：目前 95%，門檻 90%")!.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a log line")]
    [InlineData("2026-13-16T09:00:01.250+08:00 [INFO] [Monitor] 監控開始")]
    [InlineData("2026-11-16T09:00:01.250+08:00 [DEBUG] [Monitor] 監控開始")]
    public void InvalidLogLinesAreNull(string text) => Assert.Null(LogLineParser.Parse(text));

    [Fact]
    public void RowReadsTypedValues()
    {
        var row = new CsvRow(["TimestampUtc", "Value", "Empty", "Status", "Blank"], ["2026-11-16T01:00:00.500Z", "12.5", "", "Unavailable", ""]);

        Assert.Equal(new DateTimeOffset(2026, 11, 16, 1, 0, 0, 500, TimeSpan.Zero), row.Time("TimestampUtc"));
        Assert.Equal(12.5, row.Number("Value"));
        Assert.Null(row.Number("Empty"));
        Assert.Equal(MonitorStatus.Unavailable, row.Status("Status"));
        Assert.Null(row.OptionalStatus("Blank"));
        Assert.Throws<FormatException>(() => row.Status("Blank"));
        Assert.Throws<ArgumentException>(() => row.Text("Missing"));
    }

    [Theory]
    [InlineData("2026-11-16 01:00:00", "1", "OK")]
    [InlineData("2026-11-16T01:00:00.000Z", "abc", "OK")]
    [InlineData("2026-11-16T01:00:00.000Z", "NaN", "OK")]
    [InlineData("2026-11-16T01:00:00.000Z", "1", "Fine")]
    [InlineData("2026-11-16T01:00:00.000Z", "1", "3")]
    public void RowRejectsInvalidValues(string time, string number, string status)
    {
        var row = new CsvRow(["T", "N", "S"], [time, number, status]);

        Assert.ThrowsAny<FormatException>(() =>
        {
            row.Time("T");
            row.Number("N");
            row.Status("S");
        });
    }
}
