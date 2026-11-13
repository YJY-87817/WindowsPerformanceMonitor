using System.ComponentModel;
using System.Diagnostics;

namespace PerformanceMonitor.Infrastructure.Windows.Processes;

/// <summary>單一程序的 Working Set 讀數。</summary>
/// <param name="Pid">程序識別碼。</param>
/// <param name="Name">程序名稱。</param>
/// <param name="WorkingSetBytes">Working Set（位元組）。</param>
public readonly record struct ProcessReading(int Pid, string Name, long WorkingSetBytes);

/// <summary>程序列舉結果。</summary>
/// <param name="Processes">成功讀取的程序。</param>
/// <param name="SkippedCount">列舉後已結束或 PID／Working Set 無法讀取而略過的程序數。</param>
public sealed record ProcessSnapshot(IReadOnlyList<ProcessReading> Processes, int SkippedCount);

/// <summary>讀取程序資訊（測試以假物件取代）。</summary>
public interface IProcessSource
{
    /// <summary>列出所有程序的 Working Set；列舉本身失敗時擲出 <see cref="InvalidOperationException"/> 或 <see cref="Win32Exception"/>。</summary>
    ProcessSnapshot List();

    /// <summary>讀取程序啟動時間；程序已結束或無權讀取時回傳 null。</summary>
    DateTimeOffset? GetStartTimeUtc(int pid);
}

/// <summary>以 <see cref="Process"/> 實作。每個程序物件讀完即釋放。</summary>
public sealed class SystemProcessSource : IProcessSource
{
    /// <inheritdoc />
    public ProcessSnapshot List()
    {
        Process[] processes = Process.GetProcesses();
        var readings = new List<ProcessReading>(processes.Length);
        int skipped = 0;
        foreach (Process process in processes)
        {
            using (process)
            {
                try
                {
                    readings.Add(new ProcessReading(process.Id, process.ProcessName, process.WorkingSet64));
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
                {
                    skipped++;
                }
            }
        }

        return new ProcessSnapshot(readings, skipped);
    }

    /// <inheritdoc />
    public DateTimeOffset? GetStartTimeUtc(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}
