using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Settings;
using PerformanceMonitor.Infrastructure.Storage;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class StorageAndSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wpm-tests-" + Guid.NewGuid().ToString("N"));

    public StorageAndSettingsTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RelativeDirectoriesResolveUnderRoot()
    {
        var paths = new AppDataPaths(_root);

        Assert.Equal(Path.Combine(_root, "Data"), paths.ResolveDataDirectory(new LoggingSettings()));
        Assert.Equal(Path.Combine(_root, "Logs"), paths.ResolveLogDirectory(new LoggingSettings()));
        Assert.Equal(Path.Combine(_root, "settings.json"), paths.SettingsFile);
    }

    [Fact]
    public void AbsoluteDirectoriesAreKept()
    {
        string absolute = Path.Combine(_root, "elsewhere");

        Assert.Equal(absolute, new AppDataPaths(_root).ResolveDataDirectory(new LoggingSettings { DataDirectory = absolute }));
    }

    [Fact]
    public void DefaultRootIsUnderLocalApplicationData()
    {
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsPerformanceMonitor");

        Assert.Equal(Path.GetFullPath(expected), AppDataPaths.CreateDefault(_ => null).Root);
        Assert.Equal(Path.GetFullPath(expected), AppDataPaths.CreateDefault(_ => "  ").Root);
    }

    [Fact]
    public void RootCanBeOverriddenByEnvironmentVariable()
    {
        AppDataPaths paths = AppDataPaths.CreateDefault(name => name == AppDataPaths.RootVariable ? _root : null);

        Assert.Equal(Path.GetFullPath(_root), paths.Root);
        Assert.Equal(Path.Combine(Path.GetFullPath(_root), "settings.json"), paths.SettingsFile);
    }

    [Fact]
    public void Load_MissingFileUsesDefaultsWithWarning()
    {
        SettingsLoadResult result = SettingsFileLoader.Load(Path.Combine(_root, "settings.json"));

        Assert.True(result.IsValid);
        Assert.Equal(SettingsLoadResult.BuiltInDefaults, result.Source);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void Load_ReadsAndValidatesFile()
    {
        string path = Path.Combine(_root, "settings.json");
        File.WriteAllText(path, """{ "monitor": { "sampleSeconds": 5 }, "process": { "topCount": 99 } }""");

        SettingsLoadResult result = SettingsFileLoader.Load(path);

        Assert.Equal(path, result.Source);
        Assert.Single(result.Errors);
    }
}
