using System.Runtime.InteropServices;

namespace PerformanceMonitor.App.Services;

/// <summary>讓視窗標題列使用深色（Windows 10 20H1 以上與 Windows 11 支援；不支援時維持原樣，不影響功能）。</summary>
internal static partial class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;

    /// <summary>對視窗控制代碼套用深色標題列；失敗時忽略。</summary>
    public static void Apply(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
        {
            return;
        }

        try
        {
            int enabled = 1;
            _ = DwmSetWindowAttribute(windowHandle, UseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // 舊版 Windows 沒有此屬性：維持淺色標題列。
        }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
