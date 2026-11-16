using System.Text;
using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Infrastructure.Storage;

/// <summary>
/// 報告的檔案存取：CSV 與 Log 以共用讀取開啟（監控寫入中也能讀，不鎖定、不修改原始檔）；
/// 報告寫到 <c>&lt;資料根目錄&gt;\Reports</c>（UTF-8 無 BOM，HTML 自帶 charset）。
/// </summary>
public sealed class FileReportStore(AppSettings settings, AppDataPaths paths) : IReportStore
{
    /// <summary>報告資料夾名稱。</summary>
    public const string ReportsFolderName = "Reports";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public string DefaultOutputDirectory => Path.Combine(paths.Root, ReportsFolderName);

    /// <inheritdoc />
    public IEnumerable<string>? ReadLines(ReportFolder folder, string fileName)
    {
        string directory = folder == ReportFolder.Data ? paths.ResolveDataDirectory(settings.Logging) : paths.ResolveLogDirectory(settings.Logging);
        string path = Path.Combine(directory, fileName);
        return File.Exists(path) ? Read(path) : null;
    }

    /// <inheritdoc />
    public string Save(string path, string content)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, Utf8NoBom);
        return full;
    }

    private static IEnumerable<string> Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is string line)
        {
            yield return line;
        }
    }
}
