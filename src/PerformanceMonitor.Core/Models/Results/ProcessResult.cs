using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>程序 RAM Top N 結果。</summary>
/// <param name="Status">狀態；部分程序讀取失敗仍為 OK。</param>
/// <param name="Processes">依 Working Set 由大到小。</param>
/// <param name="SkippedProcessCount">列舉後結束或無法讀取而略過的程序數。</param>
/// <param name="MissingFieldCount">輔助欄位（啟動時間）無權讀取的次數。</param>
public sealed record ProcessResult(
    MonitorStatus Status,
    IReadOnlyList<ProcessItem> Processes,
    int SkippedProcessCount,
    int MissingFieldCount) : ModuleResult(ModuleNames.Process, Status);

/// <summary>單一程序。</summary>
/// <param name="Rank">排名（1 起算）。</param>
/// <param name="ProcessName">程序名稱。</param>
/// <param name="Pid">程序識別碼。</param>
/// <param name="WorkingSetMB">Working Set（MB）。</param>
/// <param name="StartTimeUtc">啟動時間；無權讀取時為 null。</param>
public sealed record ProcessItem(int Rank, string ProcessName, int Pid, double WorkingSetMB, DateTimeOffset? StartTimeUtc);
