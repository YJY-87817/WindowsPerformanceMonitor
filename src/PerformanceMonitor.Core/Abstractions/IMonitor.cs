using PerformanceMonitor.Core.Models;

namespace PerformanceMonitor.Core.Abstractions;

/// <summary>
/// 監控模組共同介面。每次取樣回傳含狀態的結果；不得擲出例外中止其他模組
/// （未預期的例外仍會被取樣流程捕捉並轉為 <c>Error</c>／<c>InternalError</c>）。
/// </summary>
public interface IMonitor
{
    /// <summary>模組名稱，例如 <c>Cpu</c>；同時作為結果的 <see cref="ModuleResult.Module"/>。</summary>
    string Name { get; }

    /// <summary>是否啟用（來自設定）。停用時不呼叫取樣，結果為 <c>Disabled</c>。</summary>
    bool IsEnabled { get; }

    /// <summary>資料來源說明，寫入啟動 Log；沒有時為 null。</summary>
    string? SourceDescription => null;

    /// <summary>建立速率型數值的基準（例如計數器第一次讀取）。預設不做事。</summary>
    Task WarmUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>取樣一次。</summary>
    Task<ModuleResult> SampleAsync(CancellationToken cancellationToken);
}
