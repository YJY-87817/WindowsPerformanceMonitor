using PerformanceMonitor.Application.Settings;

namespace PerformanceMonitor.Infrastructure.Settings;

/// <summary>
/// 讀取 settings.json。檔案不存在時使用內建預設值並警告（不自動建立檔案）；
/// 無法讀取時回報錯誤。內容解析與驗證由 <see cref="SettingsParser"/> 負責。
/// </summary>
public static class SettingsFileLoader
{
    /// <summary>讀取指定設定檔。</summary>
    public static SettingsLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return SettingsLoadResult.Missing(path);
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(new(), path, [$"無法讀取設定檔 '{path}'：{ex.Message}"], [], []);
        }

        return SettingsParser.Parse(json, path);
    }
}
