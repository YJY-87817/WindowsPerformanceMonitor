using System.Globalization;
using System.Text.RegularExpressions;

namespace PerformanceMonitor.Application.Reports;

/// <summary>一行 Log。</summary>
/// <param name="Time">時間（含時區偏移）。</param>
/// <param name="Level">等級：<c>INFO</c>、<c>WARN</c>、<c>ERROR</c>。</param>
/// <param name="Source">來源，例如 <c>Monitor</c>、<c>Alert</c>。</param>
/// <param name="Message">訊息。</param>
public sealed record LogLine(DateTimeOffset Time, string Level, string Source, string Message);

/// <summary>解析 Log 行：<c>yyyy-MM-ddTHH:mm:ss.fffzzz [LEVEL] [來源] 訊息</c>（docs/Data-Schema.md §4）。</summary>
public static partial class LogLineParser
{
    /// <summary>解析；格式不符時回傳 null。</summary>
    public static LogLine? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Match match = Pattern().Match(line.TrimStart(CsvLineParser.Bom));
        if (!match.Success
            || !DateTimeOffset.TryParseExact(match.Groups["time"].Value, "yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset time))
        {
            return null;
        }

        return new LogLine(time, match.Groups["level"].Value, match.Groups["source"].Value, match.Groups["message"].Value);
    }

    [GeneratedRegex(@"^(?<time>\S+) \[(?<level>INFO|WARN|ERROR)\] \[(?<source>[^\]]*)\] (?<message>.*)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
