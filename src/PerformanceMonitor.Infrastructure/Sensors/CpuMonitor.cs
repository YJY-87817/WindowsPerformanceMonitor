using System.Globalization;
using PerformanceMonitor.Core.Abstractions;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Core.Models;
using PerformanceMonitor.Core.Models.Results;
using PerformanceMonitor.Core.Settings;
using PerformanceMonitor.Infrastructure.Windows.Pdh;

namespace PerformanceMonitor.Infrastructure.Sensors;

/// <summary>
/// CPU 總使用率：PDH <c>\Processor(_Total)\% Processor Time</c>，以英文名稱加入，不受 Windows 語系影響。
/// 第一次讀取只建立基準（WarmingUp）；PDH 失敗時回報 Error，並在下次取樣重新建立查詢。
/// 不提供 WMI 備援（盤點 M2 決定）。
/// </summary>
public sealed class CpuMonitor(AppSettings settings, IPdhQueryFactory factory) : IMonitor, IDisposable
{
    /// <summary>計數器路徑。</summary>
    public const string CounterPath = @"\Processor(_Total)\% Processor Time";

    private readonly Lock _gate = new();
    private IPdhQuery? _query;
    private IPdhCounter? _counter;

    /// <inheritdoc />
    public string Name => ModuleNames.Cpu;

    /// <inheritdoc />
    public bool IsEnabled => settings.Cpu.Enabled;

    /// <inheritdoc />
    public string? SourceDescription => $"CPU 來源：PDH {CounterPath}（英文名稱加入，不受系統語系影響）";

    /// <inheritdoc />
    public Task WarmUpAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            try
            {
                EnsureQuery().Collect();
            }
            catch (Exception ex) when (IsExpected(ex))
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

    private CpuResult Sample()
    {
        try
        {
            IPdhQuery query = EnsureQuery();
            query.Collect();
            PdhValue value = _counter!.Read();
            return value.Status switch
            {
                PdhReadStatus.NotReady => new CpuResult(MonitorStatus.WarmingUp, null),
                PdhReadStatus.Failed => new CpuResult(MonitorStatus.Error, null)
                {
                    ErrorCode = ErrorCode.DataError,
                    ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"讀取 {CounterPath} 失敗（PDH 0x{value.ErrorCode:X8}）"),
                },
                _ when value.Value is < 0 or > 100 || double.IsNaN(value.Value) => new CpuResult(MonitorStatus.Error, null)
                {
                    ErrorCode = ErrorCode.DataError,
                    ErrorMessage = string.Create(CultureInfo.InvariantCulture, $"CPU 使用率超出 0～100：{value.Value}"),
                },
                _ => new CpuResult(MonitorStatus.OK, value.Value),
            };
        }
        catch (PdhException ex)
        {
            Reset();
            return new CpuResult(MonitorStatus.Error, null) { ErrorCode = ErrorCode.TransientError, ErrorMessage = ex.Message };
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Reset();
            return new CpuResult(MonitorStatus.Unavailable, null) { ErrorCode = ErrorCode.Unavailable, ErrorMessage = "無法載入 pdh.dll（需要 Windows）" };
        }
    }

    private IPdhQuery EnsureQuery()
    {
        if (_query is null)
        {
            IPdhQuery query = factory.Create();
            try
            {
                _counter = query.AddCounter(CounterPath);
            }
            catch
            {
                query.Dispose();
                throw;
            }

            _query = query;
        }

        return _query;
    }

    private void Reset()
    {
        _query?.Dispose();
        _query = null;
        _counter = null;
    }

    private static bool IsExpected(Exception ex) => ex is PdhException or DllNotFoundException or EntryPointNotFoundException;
}
