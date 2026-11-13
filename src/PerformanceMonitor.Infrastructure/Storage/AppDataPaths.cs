using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Infrastructure.Storage;

/// <summary>
/// 程式資料位置（決策 0005）：根目錄預設 %LOCALAPPDATA%\WindowsPerformanceMonitor；
/// 設定中的相對路徑以根目錄為基準，不依賴目前工作目錄。
/// </summary>
public sealed class AppDataPaths
{
    /// <summary>資料夾名稱。</summary>
    public const string FolderName = "WindowsPerformanceMonitor";

    /// <summary>設定檔名稱。</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>以指定根目錄建立。</summary>
    public AppDataPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>根目錄。</summary>
    public string Root { get; }

    /// <summary>設定檔完整路徑。</summary>
    public string SettingsFile => Path.Combine(Root, SettingsFileName);

    /// <summary>預設位置：%LOCALAPPDATA%\WindowsPerformanceMonitor。</summary>
    public static AppDataPaths CreateDefault() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName));

    /// <summary>CSV 目錄完整路徑。</summary>
    public string ResolveDataDirectory(LoggingSettings logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        return Resolve(logging.DataDirectory);
    }

    /// <summary>Log 目錄完整路徑。</summary>
    public string ResolveLogDirectory(LoggingSettings logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        return Resolve(logging.LogDirectory);
    }

    private string Resolve(string path) =>
        Path.IsPathFullyQualified(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(Root, path));
}
