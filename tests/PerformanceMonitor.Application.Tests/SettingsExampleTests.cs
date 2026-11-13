using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Tests;

/// <summary>docs/settings.example.json 必須有效、列出所有欄位，且數值等於內建預設值。</summary>
public class SettingsExampleTests
{
    private static string ExamplePath()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WindowsPerformanceMonitor.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "docs", "settings.example.json");
    }

    [Fact]
    public void ExampleIsCompleteAndMatchesDefaults()
    {
        string path = ExamplePath();

        SettingsLoadResult result = SettingsParser.Parse(File.ReadAllText(path), path);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.DefaultedFields);
        AppSettings defaults = new();
        Assert.Equal(defaults with { Network = result.Settings.Network }, result.Settings);
        Assert.Empty(result.Settings.Network.IncludeNames);
        Assert.Empty(result.Settings.Network.ExcludeNames);
    }
}
