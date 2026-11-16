using System.Text;
using PerformanceMonitor.Application.Settings;
using PerformanceMonitor.Core.Settings;

namespace PerformanceMonitor.Infrastructure.Settings;

/// <summary>
/// 寫入 settings.json（WPF 設定頁）：完整改寫；已有檔案時先另存為 <c>settings.json.bak</c>（覆蓋上一份備份），
/// 再以暫存檔寫入後取代，避免寫到一半留下損毀的設定檔（Phase 3c 確認）。
/// </summary>
public static class SettingsFileWriter
{
    /// <summary>備份檔副檔名。</summary>
    public const string BackupSuffix = ".bak";

    /// <summary>寫入設定。失敗時擲出 <see cref="IOException"/> 或 <see cref="UnauthorizedAccessException"/>。</summary>
    public static void Save(string path, AppSettings settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(settings);
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temp = path + ".tmp";
        File.WriteAllText(temp, SettingsSerializer.ToJson(settings), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(path))
        {
            File.Copy(path, path + BackupSuffix, overwrite: true);
        }

        File.Move(temp, path, overwrite: true);
    }
}
