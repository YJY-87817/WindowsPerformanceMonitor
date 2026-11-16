using PerformanceMonitor.Application.Reports;

namespace PerformanceMonitor.Application.Tests;

/// <summary>記憶體中的報告檔案（手寫假物件）。</summary>
internal sealed class FakeReportStore : IReportStore
{
    public Dictionary<string, List<string>> Data { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, List<string>> Logs { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Unreadable { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Saved { get; } = new(StringComparer.Ordinal);

    public string DefaultOutputDirectory => Path.Combine("root", "Reports");

    public IEnumerable<string>? ReadLines(ReportFolder folder, string fileName)
    {
        Dictionary<string, List<string>> files = folder == ReportFolder.Data ? Data : Logs;
        if (!files.TryGetValue(fileName, out List<string>? lines))
        {
            return null;
        }

        return Unreadable.Contains(fileName) ? Fail(lines) : lines;
    }

    public string Save(string path, string content)
    {
        Saved[path] = content;
        return path;
    }

    private static IEnumerable<string> Fail(List<string> lines)
    {
        yield return lines[0];
        throw new IOException("磁碟讀取錯誤");
    }
}
