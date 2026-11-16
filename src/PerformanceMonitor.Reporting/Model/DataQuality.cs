using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Model;

/// <summary>檔案問題分類。</summary>
public enum FileIssueKind
{
    /// <summary>當天有其他資料，但沒有這個檔（模組停用時沒有檔案屬正常）。</summary>
    Missing,

    /// <summary>空檔或標題列不符，整個檔略過。</summary>
    HeaderMismatch,

    /// <summary>讀取失敗（例如權限不足），整個檔或其餘部分略過。</summary>
    Unreadable,

    /// <summary>有無法解析的資料列（欄位數不對、時間或數值無法解析），已略過並計數。</summary>
    BadRows,
}

/// <summary>一個檔案問題。</summary>
/// <param name="FileName">檔名（不含目錄）。</param>
/// <param name="Kind">分類。</param>
/// <param name="Count">略過的列數（<see cref="FileIssueKind.BadRows"/>）；其他分類為 0。</param>
/// <param name="Detail">補充說明（例如第一個壞列的列號與原因）。</param>
public sealed record FileIssue(string FileName, FileIssueKind Kind, int Count, string? Detail);

/// <summary>監控時段的結束方式。</summary>
public enum SessionEnd
{
    /// <summary>正常停止（Log「監控正常停止」）。</summary>
    Stopped,

    /// <summary>異常結束（Log「監控異常結束」）。</summary>
    Faulted,

    /// <summary>單次取樣（主控台 <c>--once</c>）。</summary>
    Once,

    /// <summary>Log 沒有停止紀錄（程式被強制結束、電腦關機或仍在執行），以時段內最後一筆資料為結束。</summary>
    NotRecorded,
}

/// <summary>Application Log 中的監控事件。</summary>
public enum SessionEventKind
{
    /// <summary>監控開始。</summary>
    Started,

    /// <summary>監控正常停止。</summary>
    Stopped,

    /// <summary>監控異常結束。</summary>
    Faulted,

    /// <summary>單次取樣開始。</summary>
    OnceStarted,

    /// <summary>單次取樣完成。</summary>
    OnceCompleted,
}

/// <summary>一個監控時段（依 Application Log 的開始／停止）。</summary>
/// <param name="Start">開始；Log 中沒有開始紀錄（在期間之前開始）時為期間起點。</param>
/// <param name="End">結束。</param>
/// <param name="EndKind">結束方式。</param>
/// <param name="StartedBeforePeriod">是否在期間之前就已開始。</param>
/// <param name="ExpectedSamples">預期 CSV 紀錄筆數：⌊時長 ÷ CSV 間隔⌋ ＋ 1（開始時與停止時各寫一筆）。</param>
public sealed record MonitoringSpan(DateTimeOffset Start, DateTimeOffset End, SessionEnd EndKind, bool StartedBeforePeriod, int ExpectedSamples);

/// <summary>一個模組在 SystemMetrics 中的狀態分布。</summary>
/// <param name="Module">模組顯示名稱。</param>
/// <param name="Statuses">各狀態筆數。</param>
public sealed record ModuleAvailability(string Module, StatusCounts Statuses);

/// <summary>資料品質摘要（規劃 v2.1 §8）。</summary>
/// <param name="Earliest">最早採樣時間（SystemMetrics）。</param>
/// <param name="Latest">最晚採樣時間。</param>
/// <param name="ActualSamples">實際 SystemMetrics 紀錄筆數。</param>
/// <param name="CsvIntervalSeconds">計算預期筆數所用的 CSV 間隔（目前設定）。</param>
/// <param name="LogsFound">期間內是否有任何 Application Log（沒有時無法計算預期筆數）。</param>
/// <param name="Sessions">監控時段。</param>
/// <param name="Modules">各模組狀態分布。</param>
/// <param name="FileIssues">缺少或損毀的檔案。</param>
/// <param name="DatesWithoutData">完全沒有資料檔的日期。</param>
public sealed record DataQuality(
    DateTimeOffset? Earliest,
    DateTimeOffset? Latest,
    int ActualSamples,
    int CsvIntervalSeconds,
    bool LogsFound,
    IReadOnlyList<MonitoringSpan> Sessions,
    IReadOnlyList<ModuleAvailability> Modules,
    IReadOnlyList<FileIssue> FileIssues,
    IReadOnlyList<DateOnly> DatesWithoutData)
{
    /// <summary>預期筆數（各監控時段相加）；沒有 Log 時為 null。</summary>
    public int? ExpectedSamples => LogsFound ? Sessions.Sum(s => s.ExpectedSamples) : null;

    /// <summary>缺失比例：max(0, 預期 − 實際) ÷ 預期；無法計算時為 null。</summary>
    public double? MissingRatio => ExpectedSamples is int expected && expected > 0
        ? Math.Max(0, expected - ActualSamples) / (double)expected
        : null;
}

/// <summary>報告內容（HTML 由此產生）。</summary>
/// <param name="Period">期間。</param>
/// <param name="Timeline">時間軸與粒度。</param>
/// <param name="GeneratedAt">產生時間（本機）。</param>
/// <param name="ComputerNames">資料中的電腦名稱。</param>
/// <param name="Series">各指標統計，依分組與名稱排序。</param>
/// <param name="Quality">資料品質。</param>
public sealed record ReportModel(
    ReportPeriod Period,
    ReportTimeline Timeline,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> ComputerNames,
    IReadOnlyList<SeriesStatistics> Series,
    DataQuality Quality);
