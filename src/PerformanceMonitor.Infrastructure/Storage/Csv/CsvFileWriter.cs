using PerformanceMonitor.Core.Abstractions;

namespace PerformanceMonitor.Infrastructure.Storage.Csv;

/// <summary>
/// 附加 CSV 資料列。檔案被鎖定（例如 Excel 開啟中）時間隔 200 毫秒重試指定次數；
/// 仍失敗則回傳錯誤，由呼叫端累計遺失筆數（舊規格 v1.5 §5.5）。不擲出 I/O 例外。
/// </summary>
public sealed class CsvFileWriter(IClock clock)
{
    /// <summary>鎖定重試間隔。</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>附加資料列；成功回傳 null，失敗回傳錯誤訊息。</summary>
    /// <param name="path">檔案完整路徑。</param>
    /// <param name="header">標題列（新檔時寫入）。</param>
    /// <param name="lines">資料列（已組成文字行）。</param>
    /// <param name="retryCount">鎖定時的重試次數（0～5）。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    public async Task<string?> AppendAsync(string path, string header, IReadOnlyList<string> lines, int retryCount, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                TextFileAppender.AppendLines(path, header, lines);
                return null;
            }
            catch (IOException) when (attempt < retryCount)
            {
                await clock.DelayAsync(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                return ex.Message;
            }
        }
    }
}
