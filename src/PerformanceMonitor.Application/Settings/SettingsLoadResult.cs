using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Application.Settings;

/// <summary>
/// 設定讀取結果。有任何錯誤時不得開始監控（舊規格 v1.5 §9.1：一次列出所有錯誤）。
/// </summary>
/// <param name="Settings">實際採用的設定；有錯誤時為內建預設值，僅供顯示。</param>
/// <param name="Source">設定來源（檔案路徑，或「內建預設值」）。</param>
/// <param name="Errors">錯誤：格式錯誤、型別錯誤、超出範圍。</param>
/// <param name="Warnings">警告：設定檔不存在、未知欄位。</param>
/// <param name="DefaultedFields">設定檔中缺少、改用預設值的欄位。</param>
public sealed record SettingsLoadResult(
    AppSettings Settings,
    string Source,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> DefaultedFields)
{
    /// <summary>來源為內建預設值時的顯示文字。</summary>
    public const string BuiltInDefaults = "內建預設值";

    /// <summary>是否可以開始監控。</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>設定檔不存在時的結果：使用預設值並警告。</summary>
    public static SettingsLoadResult Missing(string path) =>
        new(new AppSettings(), BuiltInDefaults, [], [$"找不到設定檔 '{path}'，使用內建預設值。"], []);
}
