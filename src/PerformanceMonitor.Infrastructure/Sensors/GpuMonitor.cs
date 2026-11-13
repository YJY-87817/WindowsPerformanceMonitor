using System.ComponentModel;
using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Gpu;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>
/// NVIDIA GPU：每次取樣執行一次 nvidia-smi，一次查詢取得 index、名稱、使用率、顯存與溫度。
/// 尋找、逾時、解析規則沿用舊規格 v1.4 決策（只在固定位置尋找，見 <see cref="NvidiaSmiLocator"/>）：
/// 找不到工具或沒有 NVIDIA GPU 為 <c>Unavailable</c>；逾時結束程序並回報 <c>Error</c>／<c>Timeout</c>；
/// 不支援的欄位（<c>[N/A]</c> 等）留空，GPU 仍為 OK；格式錯誤或數值不合理為該 GPU <c>Error</c>／<c>DataError</c>。
/// </summary>
public sealed class GpuMonitor(AppSettings settings, INvidiaSmiLocator locator, ICommandRunner runner) : IMonitor
{
    /// <summary>nvidia-smi 參數（固定，不接受使用者輸入）。</summary>
    public static readonly IReadOnlyList<string> Arguments =
        ["--query-gpu=index,name,utilization.gpu,memory.used,memory.total,temperature.gpu", "--format=csv,noheader,nounits"];

    /// <summary>溫度合理範圍上限（°C）。</summary>
    public const double MaxTemperatureC = 150;

    private const int FieldCount = 6;

    // nvidia-smi 對不支援或暫時無法讀取的欄位輸出的文字；視為無數值而非錯誤（沿用舊版）。
    private static readonly string[] NotAvailable = ["[N/A]", "N/A", "[Not Supported]", "[Unknown Error]", "[Insufficient Permissions]"];

    /// <inheritdoc />
    public string Name => ModuleNames.Gpu;

    /// <inheritdoc />
    public bool IsEnabled => settings.Gpu.Enabled;

    /// <inheritdoc />
    public string? SourceDescription => locator.Find() is string path
        ? $"GPU 來源：nvidia-smi（{path}）"
        : locator.Candidates.Count == 0
            ? "GPU 來源：nvidia-smi（找不到；此系統沒有可搜尋的位置）"
            : $"GPU 來源：nvidia-smi（找不到；搜尋 {string.Join("、", locator.Candidates)}）";

    /// <inheritdoc />
    public async Task<ModuleResult> SampleAsync(CancellationToken cancellationToken)
    {
        string? path = locator.Find();
        if (path is null)
        {
            return new GpuResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "找不到 nvidia-smi" };
        }

        int timeoutSeconds = settings.Timeouts.GpuSeconds;
        CommandResult result;
        try
        {
            result = await runner.RunAsync(path, Arguments, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            return new GpuResult(MonitorStatus.Error, []) { ErrorCode = ErrorCode.TransientError, ErrorMessage = $"無法執行 nvidia-smi：{ex.Message}" };
        }

        return Interpret(result, timeoutSeconds);
    }

    /// <summary>由執行結果判斷狀態並解析（純函式，供測試）。</summary>
    public static GpuResult Interpret(CommandResult result, int timeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.TimedOut)
        {
            return new GpuResult(MonitorStatus.Error, [])
            {
                ErrorCode = ErrorCode.Timeout,
                ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"nvidia-smi 超過 {timeoutSeconds} 秒未回應，已結束該程序"),
            };
        }

        if (result.ExitCode != 0)
        {
            string detail = $"{result.StandardOutput}\n{result.StandardError}".Trim();
            if (detail.Contains("no devices were found", StringComparison.OrdinalIgnoreCase))
            {
                return new GpuResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "沒有 NVIDIA GPU" };
            }

            return new GpuResult(MonitorStatus.Error, [])
            {
                ErrorCode = ErrorCode.TransientError,
                ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"nvidia-smi 結束碼 {result.ExitCode}：{(detail.Length == 0 ? "（無輸出）" : detail)}"),
            };
        }

        return Parse(result.StandardOutput);
    }

    /// <summary>
    /// 解析 CSV 輸出（index, name, utilization.gpu, memory.used, memory.total, temperature.gpu）。
    /// 名稱可能含逗號：第一欄為 index、最後四欄為數值，中間合併為名稱。依 index 排序。
    /// </summary>
    public static GpuResult Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        string[] lines = [.. output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)];
        if (lines.Length == 0)
        {
            return new GpuResult(MonitorStatus.Error, []) { ErrorCode = ErrorCode.DataError, ErrorMessage = "nvidia-smi 沒有輸出任何 GPU 資料" };
        }

        GpuItem[] items = [.. lines.Select(ParseLine).OrderBy(g => g.GpuIndex < 0 ? int.MaxValue : g.GpuIndex)];
        return new GpuResult(StatusAggregator.Aggregate(items.Select(g => g.Status)), items);
    }

    private static GpuItem ParseLine(string line)
    {
        string[] fields = [.. line.Split(',').Select(f => f.Trim())];
        if (fields.Length < FieldCount)
        {
            return Failed(-1, string.Empty, string.Create(CultureInfo.InvariantCulture, $"nvidia-smi 輸出欄位數量不符（{fields.Length} 欄）"));
        }

        if (!int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int index))
        {
            return Failed(-1, string.Empty, "nvidia-smi 輸出的 GPU index 不是整數");
        }

        string name = string.Join(", ", fields[1..^4]);
        if (!TryNumber(fields[^4], out double? usage) || !TryNumber(fields[^3], out double? used)
            || !TryNumber(fields[^2], out double? total) || !TryNumber(fields[^1], out double? temperature))
        {
            return Failed(index, name, "nvidia-smi 輸出含無法解析的數值");
        }

        if (usage is < 0 or > 100)
        {
            return Failed(index, name, string.Create(CultureInfo.InvariantCulture, $"GPU 使用率不合理：{usage}"));
        }

        if (used < 0 || total < 0 || used > total)
        {
            return Failed(index, name, string.Create(CultureInfo.InvariantCulture, $"顯存使用量不合理（{used} / {total} MB）"));
        }

        if (temperature is < 0 or > MaxTemperatureC)
        {
            return Failed(index, name, string.Create(CultureInfo.InvariantCulture, $"GPU 溫度不合理：{temperature}°C"));
        }

        return new GpuItem(MonitorStatus.OK, index, name, usage, used, total, temperature);
    }

    private static bool TryNumber(string text, out double? value)
    {
        if (NotAvailable.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            value = null;
            return true;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && double.IsFinite(parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static GpuItem Failed(int index, string name, string message) =>
        new(MonitorStatus.Error, index, name, null, null, null, null) { ErrorCode = ErrorCode.DataError, ErrorMessage = message };
}
