using System.Diagnostics;
using System.Text;

namespace PerformanceMonitor.Infrastructure.Windows.Gpu;

/// <summary>尋找 nvidia-smi（測試以假物件取代）。</summary>
public interface INvidiaSmiLocator
{
    /// <summary>搜尋的位置，依序（啟動 Log 與錯誤訊息使用）。</summary>
    IReadOnlyList<string> Candidates { get; }

    /// <summary>回傳第一個存在的 nvidia-smi 完整路徑；都不存在時為 null。</summary>
    string? Find();
}

/// <summary>
/// 只在兩個固定位置尋找 nvidia-smi.exe（2026-11-13 確認，不搜尋 PATH，避免執行使用者可寫入資料夾中的同名程式）：
/// 1. 系統目錄（新版驅動程式，<c>C:\Windows\System32</c>）；
/// 2. <c>%ProgramFiles%\NVIDIA Corporation\NVSMI</c>（舊版驅動程式）。
/// 每次取樣都重新尋找，驅動程式安裝或恢復後自動回到 OK。
/// </summary>
public sealed class NvidiaSmiLocator : INvidiaSmiLocator
{
    private const string FileName = "nvidia-smi.exe";

    /// <summary>以 Windows 預設位置建立。</summary>
    public NvidiaSmiLocator()
        : this(DefaultCandidates())
    {
    }

    /// <summary>指定搜尋位置（測試用）。</summary>
    internal NvidiaSmiLocator(IReadOnlyList<string> candidates) => Candidates = candidates;

    /// <inheritdoc />
    public IReadOnlyList<string> Candidates { get; }

    /// <inheritdoc />
    public string? Find() => Candidates.FirstOrDefault(File.Exists);

    private static string[] DefaultCandidates()
    {
        var list = new List<string>();
        if (!string.IsNullOrEmpty(Environment.SystemDirectory))
        {
            list.Add(Path.Combine(Environment.SystemDirectory, FileName));
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            list.Add(Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", FileName));
        }

        return [.. list];
    }
}

/// <summary>外部命令執行結果。</summary>
/// <param name="ExitCode">結束碼（逾時時無意義）。</param>
/// <param name="StandardOutput">標準輸出。</param>
/// <param name="StandardError">標準錯誤。</param>
/// <param name="TimedOut">是否逾時（已結束該程序）。</param>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

/// <summary>執行外部命令（測試以假物件取代）。</summary>
public interface ICommandRunner
{
    /// <summary>
    /// 執行命令並等待結束；逾時或取消時結束該程序（含子程序）。
    /// 逾時回傳 <see cref="CommandResult.TimedOut"/>；取消時擲出 <see cref="OperationCanceledException"/>；
    /// 無法啟動時擲出 <see cref="System.ComponentModel.Win32Exception"/> 或 <see cref="InvalidOperationException"/>。
    /// </summary>
    Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>以 <see cref="Process"/> 執行；參數以清單傳入（不經命令列組字串），不開視窗，非同步讀取輸出避免緩衝區阻塞。</summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    /// <inheritdoc />
    public async Task<CommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = start };
        process.Start();
        Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new CommandResult(-1, string.Empty, string.Empty, TimedOut: true);
        }

        return new CommandResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false), TimedOut: false);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // 已經結束。
        }
    }
}
