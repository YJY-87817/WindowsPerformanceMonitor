using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Settings;

namespace PerformanceMonitor.Infrastructure.Tests;

public sealed class SettingsFileWriterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("wpm-settings-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void CreatesFileAndDirectoryWithoutBackup()
    {
        string path = Path.Combine(_dir, "sub", "settings.json");

        SettingsFileWriter.Save(path, new AppSettings { Process = new ProcessSettings { TopCount = 7 } });

        Assert.Equal(7, SettingsFileLoader.Load(path).Settings.Process.TopCount);
        Assert.False(File.Exists(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.NotEqual(0xEF, bytes[0]); // 不寫 BOM
    }

    [Fact]
    public void BacksUpPreviousFileIncludingInvalidOne()
    {
        string path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ 這不是 JSON");

        SettingsFileWriter.Save(path, new AppSettings());

        Assert.Equal("{ 這不是 JSON", File.ReadAllText(path + ".bak"));
        SettingsLoadResult loaded = SettingsFileLoader.Load(path);
        Assert.True(loaded.IsValid);
        Assert.Empty(loaded.Warnings);
    }

    [Fact]
    public void SecondSaveReplacesBackup()
    {
        string path = Path.Combine(_dir, "settings.json");
        SettingsFileWriter.Save(path, new AppSettings { Process = new ProcessSettings { TopCount = 3 } });
        SettingsFileWriter.Save(path, new AppSettings { Process = new ProcessSettings { TopCount = 4 } });
        SettingsFileWriter.Save(path, new AppSettings { Process = new ProcessSettings { TopCount = 5 } });

        Assert.Equal(4, SettingsParser.Parse(File.ReadAllText(path + ".bak"), "bak").Settings.Process.TopCount);
        Assert.Equal(5, SettingsFileLoader.Load(path).Settings.Process.TopCount);
    }
}
