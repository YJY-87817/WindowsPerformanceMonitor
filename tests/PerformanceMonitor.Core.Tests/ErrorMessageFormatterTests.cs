using PerformanceMonitor.Core.Common;

namespace PerformanceMonitor.Core.Tests;

public class ErrorMessageFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sanitize_BlankBecomesNull(string? message)
    {
        Assert.Null(ErrorMessageFormatter.Sanitize(message));
    }

    [Fact]
    public void Sanitize_KeepsOnlyTrimmedFirstLine()
    {
        Assert.Equal("first line", ErrorMessageFormatter.Sanitize("  first line  \r\n second line"));
    }

    [Fact]
    public void Sanitize_TruncatesTo200Characters()
    {
        string result = ErrorMessageFormatter.Sanitize(new string('x', 250))!;

        Assert.Equal(ErrorMessageFormatter.MaxLength, result.Length);
    }
}
