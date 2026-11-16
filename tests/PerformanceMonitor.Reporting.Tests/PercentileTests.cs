using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Tests;

public class PercentileTests
{
    [Fact]
    public void NearestRankPicksCeilingRank()
    {
        float[] oneToHundred = [.. Enumerable.Range(1, 100).Select(i => (float)i)];
        float[] oneToTwenty = [.. Enumerable.Range(1, 20).Select(i => (float)i)];

        Assert.Equal(95, Percentile.NearestRank(oneToHundred, 95));
        Assert.Equal(19, Percentile.NearestRank(oneToTwenty, 95)); // ⌈0.95 × 20⌉ = 19
        Assert.Equal(50, Percentile.NearestRank([15f, 20f, 35f, 40f, 50f], 95)); // ⌈4.75⌉ = 5
        Assert.Equal(35, Percentile.NearestRank([15f, 20f, 35f, 40f, 50f], 50)); // ⌈2.5⌉ = 3
    }

    [Fact]
    public void ResultIsAnActualValueAndInputIsNotModified()
    {
        float[] values = [9f, 1f, 5f];

        Assert.Equal(9, Percentile.NearestRank(values, 95));
        Assert.Equal(1, Percentile.NearestRank(values, 1));
        Assert.Equal([9f, 1f, 5f], values);
    }

    [Fact]
    public void SingleAndEmpty()
    {
        Assert.Equal(7, Percentile.NearestRank([7f], 95));
        Assert.Null(Percentile.NearestRank([], 95));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.1)]
    public void RejectsInvalidPercent(double percent) =>
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Percentile.NearestRank([1f], percent));
}
