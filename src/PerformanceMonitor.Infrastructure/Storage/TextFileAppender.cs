using System.Text;

namespace PerformanceMonitor.Infrastructure.Storage;

/// <summary>
/// 以 UTF-8 BOM 附加文字。檔案為空（新建）時先寫 BOM 與標題列；每次開檔寫完立即關閉，
/// 不長期占用檔案（使用者可隨時用 Excel 或記事本開啟）。換行一律 CRLF。
/// </summary>
internal static class TextFileAppender
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly byte[] Bom = Encoding.UTF8.GetPreamble();

    /// <summary>附加多行文字；失敗時擲出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>。</summary>
    public static void AppendLines(string path, string? headerLine, IEnumerable<string> lines)
    {
        var text = new StringBuilder();
        foreach (string line in lines)
        {
            text.Append(line).Append("\r\n");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        bool isNew = stream.Length == 0;
        stream.Seek(0, SeekOrigin.End);
        if (isNew)
        {
            stream.Write(Bom);
            if (headerLine is not null)
            {
                stream.Write(Utf8NoBom.GetBytes(headerLine + "\r\n"));
            }
        }

        stream.Write(Utf8NoBom.GetBytes(text.ToString()));
    }
}
