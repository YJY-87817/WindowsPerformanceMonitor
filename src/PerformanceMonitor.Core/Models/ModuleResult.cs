using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models;

/// <summary>
/// 監控模組一次取樣的結果。各模組以衍生 record 加上自己的數值欄位。
/// </summary>
/// <param name="Module">模組名稱。</param>
/// <param name="Status">模組狀態。</param>
public record ModuleResult(string Module, MonitorStatus Status)
{
    private readonly string? _errorMessage;

    /// <summary>錯誤分類；狀態為 <c>Error</c> 或 <c>Unavailable</c> 時提供。</summary>
    public ErrorCode? ErrorCode { get; init; }

    /// <summary>簡短錯誤訊息：只取第一行、最長 200 字元。</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        init => _errorMessage = ErrorMessageFormatter.Sanitize(value);
    }

    /// <summary>該模組取樣耗時。</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>建立停用結果。</summary>
    public static ModuleResult Disabled(string module) => new(module, MonitorStatus.Disabled);

    /// <summary>建立錯誤結果。</summary>
    public static ModuleResult Failed(string module, ErrorCode errorCode, string? message) =>
        new(module, MonitorStatus.Error) { ErrorCode = errorCode, ErrorMessage = message };

    /// <summary>建立不可用結果。</summary>
    public static ModuleResult Unavailable(string module, string? message) =>
        new(module, MonitorStatus.Unavailable) { ErrorCode = Enums.ErrorCode.Unavailable, ErrorMessage = message };
}
