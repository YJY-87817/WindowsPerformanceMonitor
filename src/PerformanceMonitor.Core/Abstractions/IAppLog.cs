namespace PerformanceMonitor.Core.Abstractions;

/// <summary>Log 檔案。</summary>
public enum LogChannel
{
    /// <summary>Application Log：程式生命週期、設定摘要、告警事件、LoopOverrun、停止。</summary>
    Application,

    /// <summary>Error Log：錯誤、不可用、恢復紀錄（已去重）。</summary>
    Error,
}

/// <summary>Log 等級。</summary>
public enum LogSeverity
{
    /// <summary>INFO。</summary>
    Info,

    /// <summary>WARN。</summary>
    Warn,

    /// <summary>ERROR。</summary>
    Error,
}

/// <summary>
/// 專用 Log 服務（舊規格 v1.5 §5.6）：一行一筆
/// <c>yyyy-MM-ddTHH:mm:ss.fffzzz [LEVEL] [來源] 訊息</c>。
/// 寫入失敗只計數，不擲出例外、不中斷監控。不得寫入機密或不必要的個人資料。
/// </summary>
public interface IAppLog
{
    /// <summary>寫入失敗次數。</summary>
    int FailureCount { get; }

    /// <summary>寫入一筆。</summary>
    void Write(LogChannel channel, LogSeverity severity, string source, string message);
}

/// <summary><see cref="IAppLog"/> 的便利方法。</summary>
public static class AppLogExtensions
{
    /// <summary>Application Log INFO。</summary>
    public static void Info(this IAppLog log, string source, string message) =>
        Guard(log).Write(LogChannel.Application, LogSeverity.Info, source, message);

    /// <summary>Application Log WARN。</summary>
    public static void Warn(this IAppLog log, string source, string message) =>
        Guard(log).Write(LogChannel.Application, LogSeverity.Warn, source, message);

    /// <summary>Application Log ERROR。</summary>
    public static void Error(this IAppLog log, string source, string message) =>
        Guard(log).Write(LogChannel.Application, LogSeverity.Error, source, message);

    private static IAppLog Guard(IAppLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        return log;
    }
}
