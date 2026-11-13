using System.ComponentModel;
using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Memory;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>
/// 實體記憶體：Win32 <c>GlobalMemoryStatusEx</c>（不需 WMI）。計算沿用舊規格 v1.5 §3.3：
/// 已使用 = 總量 − 可用；GB 為 1024 進位；使用率 = 已使用 ÷ 總量 × 100。
/// 總量為 0 或可用大於總量時為 <c>Error</c>／<c>DataError</c>，不除以 0。
/// </summary>
public sealed class MemoryMonitor(AppSettings settings, IMemoryStatusSource source) : IMonitor
{
    private const double BytesPerGB = 1_073_741_824d;

    /// <inheritdoc />
    public string Name => ModuleNames.Memory;

    /// <inheritdoc />
    public bool IsEnabled => settings.Memory.Enabled;

    /// <inheritdoc />
    public string? SourceDescription => "記憶體來源：Win32 GlobalMemoryStatusEx（可用＝ullAvailPhys）";

    /// <inheritdoc />
    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken) => Task.FromResult<ModuleResult>(Sample());

    /// <summary>由讀數計算結果（純函式，供測試）。</summary>
    public static MemoryResult Calculate(MemoryReading reading)
    {
        if (reading.TotalBytes == 0)
        {
            return Failed("實體記憶體總量為 0");
        }

        if (reading.AvailableBytes > reading.TotalBytes)
        {
            return Failed(string.Create(CultureInfo.InvariantCulture, $"可用記憶體（{reading.AvailableBytes}）大於總量（{reading.TotalBytes}）"));
        }

        double total = reading.TotalBytes;
        double free = reading.AvailableBytes;
        double used = total - free;
        return new MemoryResult(MonitorStatus.OK, total / BytesPerGB, used / BytesPerGB, free / BytesPerGB, used / total * 100);
    }

    private MemoryResult Sample()
    {
        try
        {
            return Calculate(source.Read());
        }
        catch (Win32Exception ex)
        {
            return new MemoryResult(MonitorStatus.Error, null, null, null, null)
            {
                ErrorCode = ErrorCode.TransientError,
                ErrorMessage = $"GlobalMemoryStatusEx 失敗：{ex.Message}",
            };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return new MemoryResult(MonitorStatus.Unavailable, null, null, null, null)
            {
                ErrorCode = ErrorCode.Unavailable,
                ErrorMessage = "無法呼叫 GlobalMemoryStatusEx（需要 Windows）",
            };
        }
    }

    private static MemoryResult Failed(string message) =>
        new(MonitorStatus.Error, null, null, null, null) { ErrorCode = ErrorCode.DataError, ErrorMessage = message };
}
