namespace PerformanceMonitor.Core.Common;

/// <summary>
/// 錯誤訊息整理：只取第一行、去除前後空白、最長 200 字元（舊規格 v1.5 §3.1.1）。
/// </summary>
public static class ErrorMessageFormatter
{
    /// <summary>訊息長度上限。</summary>
    public const int MaxLength = 200;

    /// <summary>整理訊息；空白或 null 回傳 null。</summary>
    public static string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string firstLine = message.Trim().Split(['\r', '\n'], 2)[0].Trim();
        return firstLine.Length <= MaxLength ? firstLine : firstLine[..MaxLength];
    }
}
