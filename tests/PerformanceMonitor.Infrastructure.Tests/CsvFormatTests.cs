using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Infrastructure.Storage.Csv;

namespace PerformanceMonitor.Infrastructure.Tests;

public class CsvFormatTests
{
    [Theory]
    [InlineData(25.5555, "25.556")]
    [InlineData(12.50, "12.5")]
    [InlineData(0.0, "0")]
    [InlineData(-0.0001, "0")]
    [InlineData(100.0, "100")]
    [InlineData(1e15, "1000000000000000")]
    [InlineData(0.0005, "0.001")]
    public void Number_UsesInvariantCultureAtMostThreeDecimals(double value, string expected)
    {
        Assert.Equal(expected, CsvFormat.Number(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Number_MissingValueIsEmptyNotZero(double? value)
    {
        Assert.Equal(string.Empty, CsvFormat.Number(value));
    }

    [Fact]
    public void OtherFormats()
    {
        Assert.Equal("12345678901", CsvFormat.WholeNumber(12345678901));
        Assert.Equal(string.Empty, CsvFormat.WholeNumber(null));
        Assert.Equal("True", CsvFormat.Boolean(true));
        Assert.Equal("False", CsvFormat.Boolean(false));
        Assert.Equal(string.Empty, CsvFormat.Boolean(null));
        Assert.Equal("WarmingUp", CsvFormat.Status(MonitorStatus.WarmingUp));
        Assert.Equal("IOError", CsvFormat.Code(ErrorCode.IOError));
        Assert.Equal(string.Empty, CsvFormat.Code(null));
    }

    [Fact]
    public void Timestamps()
    {
        var time = new DateTimeOffset(2026, 11, 13, 23, 5, 9, 7, TimeSpan.FromHours(8));

        Assert.Equal("2026-11-13T15:05:09.007Z", CsvFormat.Utc(time));
        Assert.Equal("2026-11-13T23:05:09.007+08:00", CsvFormat.Local(time));
        Assert.Equal(string.Empty, CsvFormat.Utc(null));
    }

    [Theory]
    [InlineData("chrome", "chrome")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("中文名稱", "中文名稱")]
    [InlineData(null, "")]
    public void Escape_QuotesOnlyWhenNeeded(string? value, string expected)
    {
        Assert.Equal(expected, CsvFormat.Escape(value));
    }
}
