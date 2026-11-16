using System.Globalization;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Application.Reports;

/// <summary>一列 CSV 資料，以欄位名稱取值；格式錯誤時擲出 <see cref="FormatException"/>（該列略過並計數）。</summary>
public sealed class CsvRow
{
    private readonly IReadOnlyList<string> _columns;
    private readonly string[] _fields;

    /// <summary>建立。</summary>
    public CsvRow(IReadOnlyList<string> columns, string[] fields)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(fields);
        _columns = columns;
        _fields = fields;
    }

    /// <summary>文字欄位（空欄位為空字串）。</summary>
    public string Text(string column)
    {
        int index = -1;
        for (int i = 0; i < _columns.Count; i++)
        {
            if (string.Equals(_columns[i], column, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        return index < 0 ? throw new ArgumentException($"沒有欄位 {column}", nameof(column)) : _fields[index];
    }

    /// <summary>UTC 時間（<c>yyyy-MM-ddTHH:mm:ss.fffZ</c>）。</summary>
    public DateTimeOffset Time(string column)
    {
        string text = Text(column);
        return DateTimeOffset.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value)
            ? value.ToUniversalTime()
            : throw new FormatException($"{column} 不是有效時間：{Short(text)}");
    }

    /// <summary>數值；空欄位為 null（無法取得，不是 0）。</summary>
    public double? Number(string column)
    {
        string text = Text(column);
        if (text.Length == 0)
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value)
            ? value
            : throw new FormatException($"{column} 不是有效數值：{Short(text)}");
    }

    /// <summary>狀態；空欄位為 null（快照中沒有該模組）。</summary>
    public MonitorStatus? OptionalStatus(string column)
    {
        string text = Text(column);
        if (text.Length == 0)
        {
            return null;
        }

        return Enum.TryParse(text, ignoreCase: false, out MonitorStatus status) && Enum.IsDefined(status) && !int.TryParse(text, out _)
            ? status
            : throw new FormatException($"{column} 不是有效狀態：{Short(text)}");
    }

    /// <summary>必填的狀態。</summary>
    public MonitorStatus Status(string column) =>
        OptionalStatus(column) ?? throw new FormatException($"{column} 是空的");

    private static string Short(string text) => text.Length <= 40 ? text : text[..40] + "…";
}
