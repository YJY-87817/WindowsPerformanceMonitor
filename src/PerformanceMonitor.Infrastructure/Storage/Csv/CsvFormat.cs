using System.Globalization;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Infrastructure.Storage.Csv;

/// <summary>
/// CSV 欄位格式（docs/Data-Schema.md §2）：Invariant Culture、小數最多 3 位、空值寫空欄位、
/// 依 CSV 規則跳脫逗號／雙引號／換行。
/// </summary>
public static class CsvFormat
{
    /// <summary>小數最多 3 位、省略尾端 0、不用科學記號；null 或非數字為空字串。</summary>
    public static string Number(double? value)
    {
        if (value is not double v || double.IsNaN(v) || double.IsInfinity(v))
        {
            return string.Empty;
        }

        string text = Math.Round(v, 3, MidpointRounding.AwayFromZero).ToString("0.###", CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    /// <summary>整數。</summary>
    public static string WholeNumber(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary><c>True</c>／<c>False</c>。</summary>
    public static string Boolean(bool? value) => value switch
    {
        true => "True",
        false => "False",
        null => string.Empty,
    };

    /// <summary>UTC 時間：<c>yyyy-MM-ddTHH:mm:ss.fffZ</c>。</summary>
    public static string Utc(DateTimeOffset? value) =>
        value?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>含時區偏移的本機時間：<c>yyyy-MM-ddTHH:mm:ss.fffzzz</c>。</summary>
    public static string Local(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    /// <summary>狀態名稱。</summary>
    public static string Status(MonitorStatus? value) => value?.ToString() ?? string.Empty;

    /// <summary>錯誤分類名稱。</summary>
    public static string Code(ErrorCode? value) => value?.ToString() ?? string.Empty;

    /// <summary>含逗號、雙引號或換行的文字加上雙引號，內部雙引號重複一次。</summary>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    /// <summary>將一列欄位組成一行（不含換行）。文字欄位須已用 <see cref="Escape"/> 處理。</summary>
    public static string Line(IEnumerable<string> fields) => string.Join(',', fields);
}
