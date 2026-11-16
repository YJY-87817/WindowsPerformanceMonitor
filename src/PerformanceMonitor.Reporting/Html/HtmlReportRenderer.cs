using System.Globalization;
using System.Net;
using System.Text;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Reporting.Html;

/// <summary>
/// 產生單一離線 HTML（規劃 v2.1 §8）：CSS 內嵌、不使用 JavaScript、不載入任何外部資源
/// （Content-Security-Policy 禁止外部載入）；所有來自資料或 Log 的文字一律 HTML 編碼。
/// </summary>
public static class HtmlReportRenderer
{
    private const string Style = """
        :root { color-scheme: light; --fg: #1f2328; --muted: #59636e; --line: #d1d9e0; --head: #f6f8fa; --warn: #9a6700; --bad: #d1242f; }
        body { font-family: "Segoe UI", "Microsoft JhengHei", sans-serif; color: var(--fg); background: #fff; margin: 0; padding: 24px; line-height: 1.5; }
        main { max-width: 1100px; margin: 0 auto; }
        h1 { font-size: 1.6rem; margin: 0 0 4px; }
        h2 { font-size: 1.25rem; margin: 32px 0 8px; padding-bottom: 4px; border-bottom: 1px solid var(--line); }
        h3 { font-size: 1.05rem; margin: 20px 0 6px; }
        .meta, .note { color: var(--muted); font-size: 0.9rem; }
        table { border-collapse: collapse; width: 100%; font-size: 0.9rem; margin: 6px 0 12px; }
        th, td { border: 1px solid var(--line); padding: 4px 8px; text-align: left; vertical-align: top; }
        th { background: var(--head); font-weight: 600; }
        td.num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
        .warn { color: var(--warn); }
        .bad { color: var(--bad); }
        dl { display: grid; grid-template-columns: max-content 1fr; gap: 2px 16px; margin: 6px 0 12px; }
        dt { color: var(--muted); }
        dd { margin: 0; }
        figure.chart { margin: 12px 0 20px; }
        figcaption { font-weight: 600; margin-bottom: 4px; }
        svg { width: 100%; height: auto; display: block; }
        svg .grid { stroke: var(--line); stroke-width: 1; }
        svg .axis { font-size: 11px; fill: var(--muted); }
        svg polyline { fill: none; stroke-linejoin: round; }
        svg .avg { stroke-width: 1.6; }
        svg .max { stroke-width: 1; opacity: 0.35; }
        svg .hit { fill: transparent; }
        svg .hit:hover { fill: rgba(9, 105, 218, 0.08); }
        .legend { display: flex; flex-wrap: wrap; gap: 4px 16px; margin: 4px 0; font-size: 0.85rem; }
        .legend i { display: inline-block; width: 12px; height: 3px; margin: 0 6px 3px 0; vertical-align: middle; }
        @media print { body { padding: 0; } h2 { break-after: avoid; } figure.chart { break-inside: avoid; } }
        """;

    /// <summary>產生 HTML。</summary>
    public static string Render(ReportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var html = new StringBuilder(64 * 1024);
        string title = $"效能監控報告 {model.Period}";
        html.Append("<!DOCTYPE html>\n<html lang=\"zh-Hant\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<title>").Append(H(title)).Append("</title>\n<style>\n").Append(Style).Append("</style>\n</head>\n<body>\n<main>\n");

        html.Append("<h1>").Append(H(title)).Append("</h1>\n<p class=\"meta\">");
        if (model.ComputerNames.Count > 0)
        {
            html.Append("電腦：").Append(H(string.Join("、", model.ComputerNames))).Append('　');
        }

        html.Append("產生時間：").Append(H(Time(model.GeneratedAt))).Append("　時區：").Append(H(model.Timeline.Zone.DisplayName)).Append("</p>\n");

        Statistics(html, model);
        Trends(html, model);
        Peaks(html, model);
        Alerts(html, model);
        Processes(html, model);
        Quality(html, model);

        html.Append("</main>\n</body>\n</html>\n");
        return html.ToString();
    }

    /// <summary>HTML 編碼（含引號，可用於屬性）。</summary>
    public static string H(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>數值文字：小數最多 2 位；null 為「—」。</summary>
    public static string Number(double? value) =>
        value is double v && double.IsFinite(v) ? v.ToString("#,0.##", CultureInfo.InvariantCulture) : "—";

    /// <summary>比例文字（0.1234 → 12.3%）；null 為「—」。</summary>
    public static string Ratio(double? value) =>
        value is double v && double.IsFinite(v) ? (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" : "—";

    private static string Time(DateTimeOffset time) => time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Local(ReportModel model, DateTimeOffset? time) =>
        time is DateTimeOffset t ? Time(model.Timeline.ToLocal(t)) : "—";

    private static void Statistics(StringBuilder html, ReportModel model)
    {
        html.Append("<h2>統計摘要</h2>\n");
        if (model.Series.Count == 0)
        {
            html.Append("<p>期間內沒有任何監控資料。</p>\n");
            return;
        }

        html.Append("<p class=\"note\">由 CSV 原始紀錄（每 ").Append(model.Quality.CsvIntervalSeconds)
            .Append(" 秒一筆）計算，不是彙總後的數值；只計入狀態為 OK 且有數值的紀錄，其他狀態（暖機、無法取得、錯誤、停用）與空值不列入。P95 採 Nearest Rank 法。</p>\n");

        foreach (IGrouping<string, SeriesStatistics> group in model.Series.GroupBy(s => s.Key.Group))
        {
            html.Append("<h3>").Append(H(group.Key)).Append("</h3>\n");
            if (group.Key == ReportGroups.ThermalZone)
            {
                html.Append("<p class=\"note\">ACPI 熱區是主機板回報的區域溫度，不是 CPU 核心溫度；部分電腦回報固定值。</p>\n");
            }

            html.Append("<table>\n<thead><tr><th>指標</th><th>平均</th><th>最大</th><th>P95</th><th>有效／總筆數</th><th>有效資料時間範圍</th></tr></thead>\n<tbody>\n");
            foreach (SeriesStatistics s in group)
            {
                string unit = s.Key.Unit;
                html.Append("<tr><td>").Append(H(s.Key.Name)).Append('（').Append(H(unit)).Append("）</td>")
                    .Append("<td class=\"num\">").Append(H(Number(s.Average))).Append("</td>")
                    .Append("<td class=\"num\">").Append(H(Number(s.Max))).Append("</td>")
                    .Append("<td class=\"num\">").Append(H(Number(s.P95))).Append("</td>")
                    .Append("<td class=\"num").Append(s.ValidCount == 0 ? " bad" : s.ValidCount < s.Statuses.Total ? " warn" : string.Empty).Append("\">")
                    .Append(s.ValidCount.ToString("#,0", CultureInfo.InvariantCulture)).Append(" / ")
                    .Append(s.Statuses.Total.ToString("#,0", CultureInfo.InvariantCulture)).Append("</td>")
                    .Append("<td>").Append(s.FirstValid is null ? "沒有有效資料" : H($"{Local(model, s.FirstValid)} ～ {Local(model, s.LastValid)}")).Append("</td></tr>\n");
            }

            html.Append("</tbody>\n</table>\n");
        }
    }

    private static void Trends(StringBuilder html, ReportModel model)
    {
        html.Append("<h2>趨勢</h2>\n<p class=\"note\">彙總粒度：").Append(H(model.Timeline.GranularityText))
            .Append("；滑鼠停留在圖上可看該段的平均與最大。沒有資料的時段（例如程式沒有開啟）斷開，不連線。</p>\n");
        foreach (TrendChartDefinition chart in TrendChartDefinition.All)
        {
            html.Append(SvgTrendChart.Render(chart, model.Series, model.Timeline));
        }
    }

    private static void Peaks(StringBuilder html, ReportModel model)
    {
        html.Append("<h2>尖峰時段</h2>\n");
        if (model.Peaks.Count == 0)
        {
            html.Append("<p>沒有資料。</p>\n");
            return;
        }

        html.Append("<p class=\"note\">依趨勢粒度（").Append(H(model.Timeline.GranularityText))
            .Append("）分段，取段內平均最高的前 5 個時段；相鄰的段合併為一個時段。</p>\n");
        foreach (PeakList list in model.Peaks)
        {
            bool device = list.ByDevice;
            html.Append("<h3>").Append(H(list.Title)).Append("</h3>\n<table>\n<thead><tr><th>時段</th><th>平均</th><th>最大</th>")
                .Append(device ? "<th>最高的裝置</th>" : string.Empty).Append("</tr></thead>\n<tbody>\n");
            foreach (PeakPeriod p in list.Periods)
            {
                html.Append("<tr><td>").Append(H(Range(model, p.Start, p.End))).Append("</td>")
                    .Append("<td class=\"num\">").Append(H($"{Number(p.Average)} {list.Unit}")).Append("</td>")
                    .Append("<td class=\"num\">").Append(H($"{Number(p.Max)} {list.Unit}")).Append("</td>");
                if (device)
                {
                    html.Append("<td>").Append(H(p.Device)).Append("</td>");
                }

                html.Append("</tr>\n");
            }

            html.Append("</tbody>\n</table>\n");
        }
    }

    private static void Alerts(StringBuilder html, ReportModel model)
    {
        AlertReport alerts = model.Alerts;
        html.Append("<h2>告警事件</h2>\n");
        if (alerts.TotalEvents == 0)
        {
            html.Append("<p>期間內沒有告警事件（Application Log）。</p>\n");
            return;
        }

        html.Append("<table>\n<thead><tr><th>對象</th><th>告警次數</th><th>再次通知</th><th>告警總時間</th><th>最後一次告警時的數值</th></tr></thead>\n<tbody>\n");
        foreach (AlertSummary a in alerts.Summaries)
        {
            html.Append("<tr><td>").Append(H(a.Target)).Append("</td>")
                .Append("<td class=\"num\">").Append(a.Warnings.ToString("#,0", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td class=\"num\">").Append(a.Repeats.ToString("#,0", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(H(Duration(a.Duration))).Append(a.Unrecovered ? "<span class=\"warn\">（含沒有恢復紀錄的告警，算到該次監控結束）</span>" : string.Empty).Append("</td>")
                .Append("<td>").Append(H(a.LastValueText ?? "—")).Append("</td></tr>\n");
        }

        html.Append("</tbody>\n</table>\n<h3>事件明細</h3>\n<p class=\"note\">")
            .Append(alerts.TotalEvents > alerts.Recent.Count
                ? H(string.Create(CultureInfo.InvariantCulture, $"共 {alerts.TotalEvents:#,0} 筆，列出最近 {alerts.Recent.Count:#,0} 筆（新到舊）。"))
                : H(string.Create(CultureInfo.InvariantCulture, $"共 {alerts.TotalEvents:#,0} 筆（新到舊）。")))
            .Append("</p>\n<table>\n<thead><tr><th>時間</th><th>事件</th><th>對象</th><th>數值</th><th>門檻</th></tr></thead>\n<tbody>\n");
        foreach (AlertLogEvent e in alerts.Recent)
        {
            html.Append("<tr><td>").Append(H(Local(model, e.Time))).Append("</td><td")
                .Append(e.Kind == AlertLogKind.Recovered ? ">" : " class=\"bad\">").Append(H(AlertText(e.Kind))).Append("</td>")
                .Append("<td>").Append(H(e.Target)).Append("</td><td>").Append(H(e.ValueText)).Append("</td><td>").Append(H(e.Threshold)).Append("</td></tr>\n");
        }

        html.Append("</tbody>\n</table>\n");
    }

    private static void Processes(StringBuilder html, ReportModel model)
    {
        ProcessReport p = model.Processes;
        html.Append("<h2>高記憶體程序</h2>\n");
        if (p.Records == 0)
        {
            html.Append("<p>期間內沒有程序紀錄（程序模組或程序紀錄未啟用）。</p>\n");
            return;
        }

        html.Append("<p class=\"note\">由 ProcessMetrics（每次程序紀錄的 RAM 前 N 名）依程序名稱彙總，不分 PID；同一次紀錄中的同名程序 Working Set 相加。共 ")
            .Append(p.Records.ToString("#,0", CultureInfo.InvariantCulture)).Append(" 次紀錄，依最大 Working Set 列出前 ")
            .Append(ProcessReport.MaxCount).Append(" 名。Working Set 加總不等於記憶體使用量。</p>\n")
            .Append("<table>\n<thead><tr><th>程序</th><th>進入前 N 名次數</th><th>比例</th><th>平均 (MB)</th><th>最大 (MB)</th></tr></thead>\n<tbody>\n");
        foreach (ProcessSummary s in p.Top)
        {
            html.Append("<tr><td>").Append(H(s.Name)).Append("</td>")
                .Append("<td class=\"num\">").Append(s.Appearances.ToString("#,0", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td class=\"num\">").Append(H(Ratio(s.AppearanceRatio))).Append("</td>")
                .Append("<td class=\"num\">").Append(H(Number(s.AverageMB))).Append("</td>")
                .Append("<td class=\"num\">").Append(H(Number(s.MaxMB))).Append("</td></tr>\n");
        }

        html.Append("</tbody>\n</table>\n");
    }

    /// <summary>告警事件文字。</summary>
    public static string AlertText(AlertLogKind kind) => kind switch
    {
        AlertLogKind.Warning => "告警",
        AlertLogKind.Repeat => "告警持續",
        _ => "已恢復",
    };

    /// <summary>時間長度文字，例如 <c>1 小時 5 分</c>、<c>40 秒</c>。</summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromMinutes(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)duration.TotalSeconds)} 秒");
        }

        int hours = (int)duration.TotalHours;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours:#,0} 小時 {duration.Minutes} 分")
            : string.Create(CultureInfo.InvariantCulture, $"{duration.Minutes} 分 {duration.Seconds} 秒");
    }

    private static string Range(ReportModel model, DateTimeOffset start, DateTimeOffset end)
    {
        DateTimeOffset s = model.Timeline.ToLocal(start);
        DateTimeOffset e = model.Timeline.ToLocal(end);
        string format = model.Timeline.Granularity.TotalDays >= 1 ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm";
        string endText = model.Timeline.Granularity.TotalDays >= 1
            ? e.AddDays(-1).ToString(format, CultureInfo.InvariantCulture)
            : e.ToString(s.Date == e.Date ? "HH:mm" : format, CultureInfo.InvariantCulture);
        string startText = s.ToString(format, CultureInfo.InvariantCulture);
        return startText == endText ? startText : $"{startText} ～ {endText}";
    }

    private static void Quality(StringBuilder html, ReportModel model)
    {
        DataQuality q = model.Quality;
        html.Append("<h2>資料品質</h2>\n<dl>\n");
        Item(html, "最早採樣", Local(model, q.Earliest));
        Item(html, "最晚採樣", Local(model, q.Latest));
        Item(html, "實際紀錄筆數", q.ActualSamples.ToString("#,0", CultureInfo.InvariantCulture));
        Item(html, "預期紀錄筆數", q.ExpectedSamples is int e
            ? $"{e.ToString("#,0", CultureInfo.InvariantCulture)}（各監控時段 ⌈時長 ÷ CSV 間隔 {q.CsvIntervalSeconds} 秒⌉ ＋ 1，依目前設定）"
            : "無法計算（期間內沒有 Application Log）");
        Item(html, "缺失比例", Ratio(q.MissingRatio));
        Item(html, "趨勢彙總粒度", model.Timeline.GranularityText);
        html.Append("</dl>\n");

        html.Append("<h3>監控時段</h3>\n");
        if (q.Sessions.Count == 0)
        {
            html.Append("<p>Application Log 中沒有監控開始／停止紀錄。</p>\n");
        }
        else
        {
            html.Append("<p class=\"note\">依 Application Log 的「監控開始」與停止紀錄；程式沒有開啟的時間不算缺失。</p>\n")
                .Append("<table>\n<thead><tr><th>開始</th><th>結束</th><th>結束方式</th><th>預期筆數</th></tr></thead>\n<tbody>\n");
            foreach (MonitoringSpan span in q.Sessions)
            {
                html.Append("<tr><td>").Append(H(Local(model, span.Start))).Append(span.StartedBeforePeriod ? "（期間之前已開始）" : string.Empty).Append("</td>")
                    .Append("<td>").Append(H(Local(model, span.End))).Append("</td>")
                    .Append("<td").Append(span.EndKind is SessionEnd.Faulted or SessionEnd.NotRecorded ? " class=\"warn\"" : string.Empty).Append('>')
                    .Append(H(EndText(span.EndKind))).Append("</td>")
                    .Append("<td class=\"num\">").Append(span.ExpectedSamples.ToString("#,0", CultureInfo.InvariantCulture)).Append("</td></tr>\n");
            }

            html.Append("</tbody>\n</table>\n");
        }

        html.Append("<h3>各模組狀態</h3>\n");
        if (q.Modules.Count == 0)
        {
            html.Append("<p>沒有 SystemMetrics 紀錄。</p>\n");
        }
        else
        {
            html.Append("<table>\n<thead><tr><th>模組</th><th>OK</th><th>暖機</th><th>無法取得</th><th>錯誤</th><th>停用</th><th>不可用比例</th></tr></thead>\n<tbody>\n");
            foreach (ModuleAvailability m in q.Modules)
            {
                html.Append("<tr><td>").Append(H(m.Module)).Append("</td>");
                foreach (MonitorStatus status in (MonitorStatus[])[MonitorStatus.OK, MonitorStatus.WarmingUp, MonitorStatus.Unavailable, MonitorStatus.Error, MonitorStatus.Disabled])
                {
                    html.Append("<td class=\"num\">").Append(m.Statuses[status].ToString("#,0", CultureInfo.InvariantCulture)).Append("</td>");
                }

                html.Append("<td class=\"num\">").Append(H(Ratio(m.Statuses.UnavailableRatio))).Append("</td></tr>\n");
            }

            html.Append("</tbody>\n</table>\n<p class=\"note\">不可用比例 ＝（無法取得 ＋ 錯誤）÷ 總筆數。沒有該硬體（例如沒有 NVIDIA GPU）時為 100% 屬正常。</p>\n");
        }

        html.Append("<h3>缺少或損毀的檔案</h3>\n");
        if (q.FileIssues.Count == 0 && q.DatesWithoutData.Count == 0)
        {
            html.Append("<p>沒有。</p>\n");
            return;
        }

        if (q.DatesWithoutData.Count > 0)
        {
            html.Append("<p>沒有任何資料檔的日期：").Append(H(DateRanges(q.DatesWithoutData))).Append("</p>\n");
        }

        if (q.FileIssues.Count > 0)
        {
            html.Append("<table>\n<thead><tr><th>檔案</th><th>問題</th><th>說明</th></tr></thead>\n<tbody>\n");
            foreach (FileIssue issue in q.FileIssues)
            {
                html.Append("<tr><td>").Append(H(issue.FileName)).Append("</td><td")
                    .Append(issue.Kind == FileIssueKind.Missing ? string.Empty : " class=\"bad\"").Append('>')
                    .Append(H(IssueText(issue))).Append("</td><td>").Append(H(issue.Detail)).Append("</td></tr>\n");
            }

            html.Append("</tbody>\n</table>\n<p class=\"note\">模組停用（或程序紀錄關閉）時沒有對應檔案屬正常。報告不會修改或刪除原始檔案。</p>\n");
        }
    }

    private static void Item(StringBuilder html, string label, string value) =>
        html.Append("<dt>").Append(H(label)).Append("</dt><dd>").Append(H(value)).Append("</dd>\n");

    /// <summary>結束方式文字。</summary>
    public static string EndText(SessionEnd end) => end switch
    {
        SessionEnd.Stopped => "正常停止",
        SessionEnd.Faulted => "異常結束",
        SessionEnd.Once => "單次取樣",
        _ => "未記錄停止（以最後一筆資料為結束）",
    };

    /// <summary>檔案問題文字。</summary>
    public static string IssueText(FileIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return issue.Kind switch
        {
            FileIssueKind.Missing => "缺少",
            FileIssueKind.HeaderMismatch => "標題列不符或空檔，整個檔略過",
            FileIssueKind.Unreadable => "無法讀取",
            _ => string.Create(CultureInfo.InvariantCulture, $"略過 {issue.Count:#,0} 列無法解析的資料"),
        };
    }

    /// <summary>連續日期合併為區間，例如 <c>2026-11-01 ～ 2026-11-03、2026-11-05</c>。</summary>
    public static string DateRanges(IReadOnlyList<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var parts = new List<string>();
        DateOnly[] sorted = [.. dates.Order()];
        int i = 0;
        while (i < sorted.Length)
        {
            int j = i;
            while (j + 1 < sorted.Length && sorted[j + 1].DayNumber == sorted[j].DayNumber + 1)
            {
                j++;
            }

            parts.Add(i == j
                ? sorted[i].ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{sorted[i]:yyyy-MM-dd} ～ {sorted[j]:yyyy-MM-dd}"));
            i = j + 1;
        }

        return string.Join("、", parts);
    }
}
