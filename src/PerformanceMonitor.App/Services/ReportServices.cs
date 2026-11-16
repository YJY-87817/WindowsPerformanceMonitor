using System.Diagnostics;
using System.IO;
using PerformanceMonitor.Application.Reports;
using PerformanceMonitor.Presentation.ViewModels;
using PerformanceMonitor.Reporting.Model;

namespace PerformanceMonitor.App.Services;

/// <summary>在背景執行緒產生報告（讀檔與統計不阻塞 UI；監控照常）。</summary>
public sealed class ReportRunner(ReportService service, IReportStore store) : IReportRunner
{
    /// <inheritdoc />
    public string OutputDirectory => store.DefaultOutputDirectory;

    /// <inheritdoc />
    public Task<ReportResult> GenerateAsync(ReportPeriod period) => Task.Run(() => service.Generate(period));
}

/// <summary>以系統預設程式開啟檔案（報告用預設瀏覽器）或資料夾（檔案總管）。</summary>
public sealed class ShellLauncher : IShellLauncher
{
    /// <inheritdoc />
    public void Open(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            if (Path.HasExtension(path))
            {
                throw new FileNotFoundException("報告檔已不存在", path);
            }

            Directory.CreateDirectory(path); // 尚未產生過報告時先建立資料夾
        }

        using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }))
        {
        }
    }
}
