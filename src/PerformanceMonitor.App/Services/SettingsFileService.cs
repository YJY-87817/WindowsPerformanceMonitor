using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Settings;
using PerformanceMonitor.Infrastructure.Storage;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.App.Services;

/// <summary>設定頁的設定檔存取：寫入 settings.json（舊檔另存 .bak）、檢查資料目錄。</summary>
public sealed class SettingsFileService(AppDataPaths paths) : ISettingsService
{
    /// <inheritdoc />
    public string SettingsPath => paths.SettingsFile;

    /// <inheritdoc />
    public IReadOnlyList<string> CheckDirectories(AppSettings settings) => StorageInitializer.Check(settings, paths);

    /// <inheritdoc />
    public void Save(AppSettings settings) => SettingsFileWriter.Save(paths.SettingsFile, settings);
}
