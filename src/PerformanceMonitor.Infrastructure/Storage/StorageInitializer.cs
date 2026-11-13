using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Infrastructure.Storage;

/// <summary>
/// 啟動前確認 CSV 與 Log 目錄可寫入：建立目錄後實際寫入並刪除一個暫存檔。
/// 任一目錄失敗時不得開始監控（決策 0005），也不自動改寫到其他位置。
/// </summary>
public static class StorageInitializer
{
    /// <summary>檢查目錄；回傳錯誤清單，全部可寫入時為空。CSV 停用時不檢查 CSV 目錄。</summary>
    public static IReadOnlyList<string> Check(AppSettings settings, AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);
        var errors = new List<string>();

        if (CheckWritable(paths.ResolveLogDirectory(settings.Logging)) is string logError)
        {
            errors.Add($"Log 目錄無法寫入：{logError}");
        }

        if (settings.Logging.Enabled && CheckWritable(paths.ResolveDataDirectory(settings.Logging)) is string dataError)
        {
            errors.Add($"CSV 目錄無法寫入：{dataError}");
        }

        return errors;
    }

    /// <summary>確認單一目錄可寫入；成功回傳 null，失敗回傳含路徑的原因。</summary>
    public static string? CheckWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return $"'{directory}'：{ex.Message}";
        }
    }
}
