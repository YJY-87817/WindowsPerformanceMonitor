using System.ComponentModel;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Processes;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>
/// 程序 RAM Top N（舊規格 v1.5 §3.7）：依 Working Set 由大到小取前 <c>process.topCount</c> 名。
/// 列舉後結束或無法讀取的程序略過並計數；啟動時間（輔助辨識 PID 重用）只讀前 N 名，無權讀取時留空並計數。
/// 部分讀取失敗屬常態，狀態仍為 OK；只有列舉失敗或沒有任何可讀程序時為 Error。
/// 程序名稱可能揭露使用者使用的軟體，只寫入本機 CSV（§7.4）。
/// </summary>
public sealed class ProcessMonitor(AppSettings settings, IProcessSource source) : IMonitor
{
    private const double BytesPerMB = 1_048_576d;

    /// <inheritdoc />
    public string Name => ModuleNames.Process;

    /// <inheritdoc />
    public bool IsEnabled => settings.Process.Enabled;

    /// <inheritdoc />
    public string? SourceDescription => $"程序來源：System.Diagnostics.Process（Working Set Top {settings.Process.TopCount}）";

    /// <inheritdoc />
    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken)
    {
        ProcessSnapshot snapshot;
        try
        {
            snapshot = source.List();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return Task.FromResult<ModuleResult>(new ProcessResult(MonitorStatus.Error, [], 0, 0)
            {
                ErrorCode = ErrorCode.TransientError,
                ErrorMessage = $"列舉程序失敗：{ex.Message}",
            });
        }

        return Task.FromResult<ModuleResult>(Select(snapshot, settings.Process.TopCount, source.GetStartTimeUtc));
    }

    /// <summary>由列舉結果選出 Top N（純函式，供測試）。</summary>
    public static ProcessResult Select(ProcessSnapshot snapshot, int topCount, Func<int, DateTimeOffset?> startTime)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(startTime);
        if (snapshot.Processes.Count == 0)
        {
            return new ProcessResult(MonitorStatus.Error, [], snapshot.SkippedCount, 0)
            {
                ErrorCode = ErrorCode.AccessDenied,
                ErrorMessage = "沒有任何可讀取的程序",
            };
        }

        ProcessReading[] top = [.. snapshot.Processes.OrderByDescending(p => p.WorkingSetBytes).ThenBy(p => p.Pid).Take(topCount)];
        int missing = 0;
        var items = new List<ProcessItem>(top.Length);
        for (int i = 0; i < top.Length; i++)
        {
            DateTimeOffset? started = startTime(top[i].Pid);
            if (started is null)
            {
                missing++;
            }

            items.Add(new ProcessItem(i + 1, top[i].Name, top[i].Pid, top[i].WorkingSetBytes / BytesPerMB, started));
        }

        return new ProcessResult(MonitorStatus.OK, items, snapshot.SkippedCount, missing);
    }
}
