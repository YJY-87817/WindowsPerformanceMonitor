using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;

namespace PerformanceMonitor.Core.Models.Results;

/// <summary>
/// 多裝置模組中單一裝置（磁碟、GPU、介面卡、感測器）的資料；各裝置有自己的狀態。
/// </summary>
/// <param name="Status">該裝置狀態。</param>
public abstract record DeviceItem(MonitorStatus Status)
{
    private readonly string? _errorMessage;

    /// <summary>識別該裝置的名稱，用於 Log 與錯誤去重（例如 <c>C:</c>、<c>GPU 0</c>）。</summary>
    public abstract string DeviceId { get; }

    /// <summary>錯誤分類。</summary>
    public ErrorCode? ErrorCode { get; init; }

    /// <summary>簡短錯誤訊息：只取第一行、最長 200 字元。</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        init => _errorMessage = ErrorMessageFormatter.Sanitize(value);
    }
}

/// <summary>含多個裝置的模組結果。</summary>
public interface IDeviceResult
{
    /// <summary>各裝置資料。</summary>
    IEnumerable<DeviceItem> Devices { get; }
}
