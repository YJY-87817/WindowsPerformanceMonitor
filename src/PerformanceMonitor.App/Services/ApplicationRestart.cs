using System.Diagnostics;
using PerformanceMonitor.Application.Monitoring;
using PerformanceMonitor.Presentation.ViewModels;

namespace PerformanceMonitor.App.Services;

/// <summary>
/// 套用設定（Phase 3c 確認）：停止監控（補寫最後一筆、寫停止 Log）→ 以相同參數啟動新的程式 → 結束目前程式。
/// </summary>
public sealed class ApplicationRestart(MonitoringController controller) : IApplicationRestart
{
    /// <inheritdoc />
    public async Task RestartAsync()
    {
        await controller.StopAsync().ConfigureAwait(true);
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("無法取得程式路徑");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        using (Process.Start(start))
        {
        }

        System.Windows.Application.Current.Shutdown();
    }
}
