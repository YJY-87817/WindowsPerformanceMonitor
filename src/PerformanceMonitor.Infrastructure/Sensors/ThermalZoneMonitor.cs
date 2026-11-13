using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Common;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>
/// ACPI 熱區溫度：PDH <c>\Thermal Zone Information(*)</c>（一般使用者可讀，不使用需要管理員權限的 WMI）。
/// 優先使用 <c>High Precision Temperature</c>（0.1 K，Windows 10 起），沒有或讀不到時改用 <c>Temperature</c>（K）（2026-11-13 確認）。
/// 這不是 CPU 核心溫度；部分電腦回報固定值，不偵測，只在文件與畫面說明（2026-11-13 確認）。
/// </summary>
public sealed class ThermalZoneMonitor(AppSettings settings, IPdhQueryFactory factory) : IMonitor, IDisposable
{
    /// <summary>高精度計數器（0.1 K）。</summary>
    public const string HighPrecisionPath = @"\Thermal Zone Information(*)\High Precision Temperature";

    /// <summary>整數計數器（K）。</summary>
    public const string TemperaturePath = @"\Thermal Zone Information(*)\Temperature";

    /// <summary>合理溫度下限（°C）。</summary>
    public const double MinTemperatureC = -40;

    /// <summary>合理溫度上限（°C）。</summary>
    public const double MaxTemperatureC = 150;

    private const double KelvinOffset = 273.15;

    private readonly Lock _gate = new();
    private IPdhQuery? _query;
    private IPdhCounterArray? _highPrecision;
    private IPdhCounterArray? _temperature;

    /// <inheritdoc />
    public string Name => ModuleNames.Temperature;

    /// <inheritdoc />
    public bool IsEnabled => settings.Temperature.Enabled;

    /// <inheritdoc />
    public string? SourceDescription =>
        @"ACPI 熱區來源：PDH \Thermal Zone Information(*)\High Precision Temperature（沒有時用 Temperature）；不是 CPU 核心溫度，部分電腦回報固定值";

    /// <inheritdoc />
    public Task WarmUpAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            try
            {
                EnsureQuery()?.Collect();
            }
            catch (Exception ex) when (ex is PdhException or DllNotFoundException or EntryPointNotFoundException)
            {
                Reset();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ModuleResult> SampleAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<ModuleResult>(Sample());
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            Reset();
        }
    }

    /// <summary>由兩個計數器的讀值計算結果（純函式，供測試）；高精度計數器不存在時傳 null。</summary>
    public static TemperatureResult Calculate(PdhArrayValue? highPrecision, PdhArrayValue temperature)
    {
        ArgumentNullException.ThrowIfNull(temperature);
        string[] names =
        [
            .. (highPrecision?.Instances.Keys ?? []).Concat(temperature.Instances.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];

        if (names.Length == 0)
        {
            // 以整數計數器（所有系統都有）判斷整體狀態。
            return temperature.Status switch
            {
                PdhReadStatus.Failed => new TemperatureResult(MonitorStatus.Error, [])
                {
                    ErrorCode = ErrorCode.DataError,
                    ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"讀取熱區計數器失敗（PDH 0x{temperature.ErrorCode:X8}）"),
                },
                // 溫度是瞬時值（不需兩次取樣），沒有任何執行個體又沒有資料時代表系統有熱區物件但沒有熱區
                // （Windows CI 的 VM 回傳 PDH 無資料），不是暖機中。
                PdhReadStatus.NotReady => new TemperatureResult(MonitorStatus.Unavailable, [])
                {
                    ErrorCode = ErrorCode.Unavailable,
                    ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"沒有 ACPI 熱區（PDH 0x{temperature.ErrorCode:X8}）"),
                },
                _ => new TemperatureResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "沒有 ACPI 熱區" },
            };
        }

        ThermalZoneItem[] items = [.. names.Select(n => Item(n, Lookup(highPrecision, n), Lookup(temperature, n)))];
        return new TemperatureResult(StatusAggregator.Aggregate(items.Select(i => i.Status)), items);
    }

    private static PdhValue? Lookup(PdhArrayValue? array, string name) =>
        array is { Status: PdhReadStatus.Ok } && array.Instances.TryGetValue(name, out PdhValue value) ? value : null;

    private static ThermalZoneItem Item(string name, PdhValue? highPrecision, PdhValue? temperature)
    {
        double kelvin;
        if (highPrecision is { Status: PdhReadStatus.Ok } hp)
        {
            kelvin = hp.Value / 10;
        }
        else if (temperature is { Status: PdhReadStatus.Ok } t)
        {
            kelvin = t.Value;
        }
        else if (highPrecision?.Status == PdhReadStatus.Failed || temperature?.Status == PdhReadStatus.Failed)
        {
            uint code = (highPrecision?.Status == PdhReadStatus.Failed ? highPrecision : temperature)!.Value.ErrorCode;
            return Failed(name, string.Create(CultureInfo.InvariantCulture, $"讀取熱區溫度失敗（PDH 0x{code:X8}）"));
        }
        else
        {
            return new ThermalZoneItem(MonitorStatus.WarmingUp, name, null);
        }

        double celsius = kelvin - KelvinOffset;
        if (!double.IsFinite(celsius) || celsius < MinTemperatureC || celsius > MaxTemperatureC)
        {
            return Failed(name, string.Create(CultureInfo.InvariantCulture, $"熱區溫度不合理：{kelvin} K"));
        }

        return new ThermalZoneItem(MonitorStatus.OK, name, Math.Round(celsius, 2));
    }

    private static ThermalZoneItem Failed(string name, string message) =>
        new(MonitorStatus.Error, name, null) { ErrorCode = ErrorCode.DataError, ErrorMessage = message };

    private TemperatureResult Sample()
    {
        try
        {
            IPdhQuery? query = EnsureQuery();
            if (query is null)
            {
                return new TemperatureResult(MonitorStatus.Unavailable, [])
                {
                    ErrorCode = ErrorCode.Unavailable,
                    ErrorMessage = "沒有 ACPI 熱區（系統沒有 Thermal Zone Information 計數器）",
                };
            }

            query.Collect();
            return Calculate(_highPrecision?.ReadArray(), _temperature!.ReadArray());
        }
        catch (PdhException ex)
        {
            Reset();
            return new TemperatureResult(MonitorStatus.Error, []) { ErrorCode = ErrorCode.TransientError, ErrorMessage = ex.Message };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Reset();
            return new TemperatureResult(MonitorStatus.Unavailable, []) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "無法載入 pdh.dll（需要 Windows）" };
        }
    }

    /// <summary>建立查詢；整數計數器也無法加入（沒有熱區物件）時回傳 null，下次取樣再試。</summary>
    private IPdhQuery? EnsureQuery()
    {
        if (_query is not null)
        {
            return _query;
        }

        IPdhQuery query = factory.Create();
        try
        {
            try
            {
                _temperature = query.AddCounterArray(TemperaturePath);
            }
            catch (PdhException)
            {
                query.Dispose();
                return null;
            }

            try
            {
                _highPrecision = query.AddCounterArray(HighPrecisionPath);
            }
            catch (PdhException)
            {
                _highPrecision = null; // Windows 10 以前沒有高精度計數器
            }
        }
        catch
        {
            query.Dispose();
            throw;
        }

        _query = query;
        return _query;
    }

    private void Reset()
    {
        _query?.Dispose();
        _query = null;
        _highPrecision = null;
        _temperature = null;
    }
}
