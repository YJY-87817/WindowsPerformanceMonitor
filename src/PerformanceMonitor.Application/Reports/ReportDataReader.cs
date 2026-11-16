using System.Globalization;
using PerformanceMonitor.Core.Enums;
using PerformanceMonitor.Reporting;
using PerformanceMonitor.Reporting.Model;
using PerformanceMonitor.Reporting.Statistics;

namespace PerformanceMonitor.Application.Reports;

/// <summary>
/// 讀取期間內每天的 Application Log 與 CSV，逐筆交給 <see cref="ReportBuilder"/>（串流處理，不把整個檔讀進記憶體）。
/// 壞列（欄位數不對、時間／數值／狀態無法解析）略過並計數；空檔或標題列不符的檔整個略過；
/// 讀取失敗的檔記為無法讀取（2026-11-16 確認）。不修改任何原始檔。
/// </summary>
public sealed class ReportDataReader(IReportStore store)
{
    private const string MonitorSource = "Monitor";
    private const string AlertSource = "Alert";
    private const string RepeatPrefix = "告警持續（冷卻結束後再次通知） ";
    private const string WarningPrefix = "告警 ";
    private const string RecoveredPrefix = "已恢復 ";

    /// <summary>讀取並交給 <paramref name="builder"/>。</summary>
    public void Read(ReportPeriod period, ReportBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(builder);

        // 先讀 Log（監控時段），再讀 CSV（未記錄停止的時段以最後一筆資料為結束）。
        var logDates = new HashSet<DateOnly>();
        foreach (DateOnly date in period.Dates)
        {
            if (ReadLog(date, builder))
            {
                logDates.Add(date);
            }
        }

        foreach (DateOnly date in period.Dates)
        {
            ReadDay(date, builder, logDates.Contains(date));
        }
    }

    private static SessionEventKind? SessionEvent(LogLine line)
    {
        if (!string.Equals(line.Source, MonitorSource, StringComparison.Ordinal))
        {
            return null;
        }

        string m = line.Message;
        return m.StartsWith("監控開始", StringComparison.Ordinal) ? SessionEventKind.Started
            : m.StartsWith("監控正常停止", StringComparison.Ordinal) ? SessionEventKind.Stopped
            : m.StartsWith("監控異常結束", StringComparison.Ordinal) ? SessionEventKind.Faulted
            : m.StartsWith("單次取樣開始", StringComparison.Ordinal) ? SessionEventKind.OnceStarted
            : m.StartsWith("單次取樣完成", StringComparison.Ordinal) ? SessionEventKind.OnceCompleted
            : null;
    }

    /// <summary>
    /// 解析告警事件訊息：<c>告警 CPU：目前 95%，門檻 90%</c>、<c>告警持續（冷卻結束後再次通知） …</c>、<c>已恢復 …</c>
    /// （AlertService 的寫法）；不是告警事件或格式不符時回傳 null。
    /// </summary>
    public static AlertLogEvent? ParseAlert(LogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!string.Equals(line.Source, AlertSource, StringComparison.Ordinal))
        {
            return null;
        }

        string m = line.Message;
        (AlertLogKind kind, string rest) = m.StartsWith(RepeatPrefix, StringComparison.Ordinal) ? (AlertLogKind.Repeat, m[RepeatPrefix.Length..])
            : m.StartsWith(WarningPrefix, StringComparison.Ordinal) ? (AlertLogKind.Warning, m[WarningPrefix.Length..])
            : m.StartsWith(RecoveredPrefix, StringComparison.Ordinal) ? (AlertLogKind.Recovered, m[RecoveredPrefix.Length..])
            : (AlertLogKind.Warning, string.Empty);
        int valueStart = rest.IndexOf("：目前 ", StringComparison.Ordinal);
        int thresholdStart = rest.LastIndexOf("，門檻 ", StringComparison.Ordinal);
        if (rest.Length == 0 || valueStart <= 0 || thresholdStart < valueStart)
        {
            return null;
        }

        return new AlertLogEvent(
            line.Time,
            kind,
            rest[..valueStart],
            rest[(valueStart + "：目前 ".Length)..thresholdStart],
            rest[(thresholdStart + "，門檻 ".Length)..]);
    }

    private bool ReadLog(DateOnly date, ReportBuilder builder)
    {
        string name = ReportCsvFile.ApplicationLogName(date);
        IEnumerable<string>? lines = store.ReadLines(ReportFolder.Logs, name);
        if (lines is null)
        {
            return false;
        }

        builder.MarkLogsFound();
        try
        {
            foreach (string text in lines)
            {
                if (LogLineParser.Parse(text) is not LogLine line)
                {
                    continue;
                }

                if (SessionEvent(line) is SessionEventKind kind)
                {
                    builder.AddSessionEvent(line.Time, kind);
                }
                else if (ParseAlert(line) is AlertLogEvent alert)
                {
                    builder.AddAlertEvent(alert);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            builder.AddFileIssue(new FileIssue(name, FileIssueKind.Unreadable, 0, ex.Message));
        }

        return true;
    }

    private void ReadDay(DateOnly date, ReportBuilder builder, bool hasLog)
    {
        var missing = new List<string>();
        bool any = false;
        var network = new NetworkTotals(builder);

        foreach (ReportCsvFile file in ReportCsvFile.All)
        {
            Action<CsvRow> handle = file.Name switch
            {
                "SystemMetrics" => row => SystemRow(row, builder),
                "DiskMetrics" => row => Disk(row, builder),
                "DiskIoMetrics" => row => DiskIo(row, builder),
                "GpuMetrics" => row => Gpu(row, builder),
                "NetworkMetrics" => network.Add,
                "ProcessMetrics" => row => Process(row, builder),
                _ => row => Temperature(row, builder),
            };

            if (ReadCsv(file, date, builder, handle))
            {
                any = true;
            }
            else
            {
                missing.Add(file.FileName(date));
            }

            if (file == ReportCsvFile.NetworkMetrics)
            {
                network.Flush();
            }
        }

        if (!any)
        {
            builder.AddDateWithoutData(date);
            return;
        }

        foreach (string name in missing)
        {
            builder.AddFileIssue(new FileIssue(name, FileIssueKind.Missing, 0, null));
        }

        if (!hasLog)
        {
            builder.AddFileIssue(new FileIssue(ReportCsvFile.ApplicationLogName(date), FileIssueKind.Missing, 0, "無法判斷這天的監控時段"));
        }
    }

    /// <summary>讀一個 CSV；檔案不存在時回傳 false。</summary>
    private bool ReadCsv(ReportCsvFile file, DateOnly date, ReportBuilder builder, Action<CsvRow> handle)
    {
        string name = file.FileName(date);
        IEnumerable<string>? lines = store.ReadLines(ReportFolder.Data, name);
        if (lines is null)
        {
            return false;
        }

        int lineNumber = 0;
        int badRows = 0;
        string? firstBad = null;
        try
        {
            foreach (string text in lines)
            {
                lineNumber++;
                if (lineNumber == 1)
                {
                    if (!string.Equals(text.TrimStart(CsvLineParser.Bom), string.Join(',', file.Columns), StringComparison.Ordinal))
                    {
                        builder.AddFileIssue(new FileIssue(name, FileIssueKind.HeaderMismatch, 0, "標題列與 schema v1 不符"));
                        return true;
                    }

                    continue;
                }

                if (text.Length == 0)
                {
                    continue;
                }

                try
                {
                    string[] fields = CsvLineParser.Split(text) ?? throw new FormatException("引號格式錯誤");
                    if (fields.Length != file.Columns.Count)
                    {
                        throw new FormatException(string.Create(CultureInfo.InvariantCulture, $"欄位數 {fields.Length}，應為 {file.Columns.Count}"));
                    }

                    handle(new CsvRow(file.Columns, fields));
                }
                catch (FormatException ex)
                {
                    badRows++;
                    firstBad ??= string.Create(CultureInfo.InvariantCulture, $"第一個在第 {lineNumber} 行：{ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            builder.AddFileIssue(new FileIssue(name, FileIssueKind.Unreadable, 0, ex.Message));
            return true;
        }

        if (lineNumber == 0)
        {
            builder.AddFileIssue(new FileIssue(name, FileIssueKind.HeaderMismatch, 0, "空檔"));
        }
        else if (badRows > 0)
        {
            builder.AddFileIssue(new FileIssue(name, FileIssueKind.BadRows, badRows, firstBad));
        }

        return true;
    }

    private static void SystemRow(CsvRow row, ReportBuilder builder)
    {
        DateTimeOffset time = row.Time("TimestampUtc");
        MonitorStatus? cpu = row.OptionalStatus("CpuStatus");
        MonitorStatus? memory = row.OptionalStatus("MemoryStatus");
        double? cpuPercent = row.Number("CpuUsagePercent");
        double? memoryPercent = row.Number("MemoryUsagePercent");
        double? memoryUsed = row.Number("MemoryUsedGB");
        (string Module, MonitorStatus? Status)[] modules =
        [
            (ReportGroups.Cpu, cpu), (ReportGroups.Memory, memory),
            (ReportGroups.Disk, row.OptionalStatus("DiskStatus")), (ReportGroups.DiskIo, row.OptionalStatus("DiskIoStatus")),
            (ReportGroups.Gpu, row.OptionalStatus("GpuStatus")), (ReportGroups.Network, row.OptionalStatus("NetworkStatus")),
            (ReportGroups.Process, row.OptionalStatus("ProcessStatus")), (ReportGroups.ThermalZone, row.OptionalStatus("TemperatureStatus")),
        ];

        // 整列解析成功才加入，避免壞列只算一半。
        builder.AddSample(time, row.Text("ComputerName"));
        foreach ((string module, MonitorStatus? status) in modules)
        {
            if (status is MonitorStatus s)
            {
                builder.AddModuleStatus(module, s);
            }
        }

        if (cpu is MonitorStatus c)
        {
            builder.AddMetric(new SeriesKey(ReportGroups.Cpu, "使用率", "%", SeriesRole.CpuUsage, "CPU"), time, cpuPercent, c);
        }

        if (memory is MonitorStatus m)
        {
            builder.AddMetric(new SeriesKey(ReportGroups.Memory, "使用率", "%", SeriesRole.MemoryUsage, "記憶體"), time, memoryPercent, m);
            builder.AddMetric(new SeriesKey(ReportGroups.Memory, "已用", "GB"), time, memoryUsed, m);
        }
    }

    private static void Disk(CsvRow row, ReportBuilder builder)
    {
        DateTimeOffset time = row.Time("TimestampUtc");
        MonitorStatus status = row.Status("Status");
        double? used = row.Number("UsedPercent");
        string drive = row.Text("Drive");
        if (drive.Length > 0)
        {
            builder.AddMetric(new SeriesKey(ReportGroups.Disk, $"{drive} 已用", "%"), time, used, status);
        }
    }

    private static void DiskIo(CsvRow row, ReportBuilder builder)
    {
        DateTimeOffset time = row.Time("TimestampUtc");
        MonitorStatus status = row.Status("Status");
        string disk = row.Text("Disk");
        (string Name, string Unit, double? Value)[] metrics =
        [
            ("忙碌", "%", row.Number("BusyPercent")),
            ("讀取", "MB/s", row.Number("ReadMBps")),
            ("寫入", "MB/s", row.Number("WriteMBps")),
            ("讀取延遲", "ms", row.Number("ReadLatencyMs")),
            ("寫入延遲", "ms", row.Number("WriteLatencyMs")),
        ];
        string scope = row.Text("Scope");
        if (disk.Length == 0)
        {
            return;
        }

        string label = scope switch
        {
            "Logical" => disk,
            "Physical" => $"實體磁碟 {disk}",
            _ => throw new FormatException($"Scope 不是有效值：{scope}"),
        };
        foreach ((string name, string unit, double? value) in metrics)
        {
            SeriesRole role = scope == "Logical" && name == "忙碌" ? SeriesRole.LogicalDiskBusy : SeriesRole.None;
            builder.AddMetric(new SeriesKey(ReportGroups.DiskIo, $"{label} {name}", unit, role, label), time, value, status);
        }
    }

    private static void Gpu(CsvRow row, ReportBuilder builder)
    {
        DateTimeOffset time = row.Time("TimestampUtc");
        MonitorStatus status = row.Status("Status");
        double? usage = row.Number("UsagePercent");
        double? memory = row.Number("MemoryUsedMB");
        string index = row.Text("GpuIndex");
        if (index.Length == 0)
        {
            return;
        }

        string label = $"GPU {index} {row.Text("GpuName")}".TrimEnd();
        builder.AddMetric(new SeriesKey(ReportGroups.Gpu, $"{label} 使用率", "%", SeriesRole.GpuUsage, label), time, usage, status);
        builder.AddMetric(new SeriesKey(ReportGroups.Gpu, $"{label} 顯存已用", "MB"), time, memory, status);
    }

    private static void Process(CsvRow row, ReportBuilder builder)
    {
        row.Time("TimestampUtc");
        MonitorStatus status = row.Status("Status");
        double? workingSet = row.Number("WorkingSetMB");
        string name = row.Text("ProcessName");
        if (status == MonitorStatus.OK && name.Length > 0 && workingSet is double mb)
        {
            builder.AddProcess(row.Text("SampleId"), name, mb);
        }
    }

    private static void Temperature(CsvRow row, ReportBuilder builder)
    {
        DateTimeOffset time = row.Time("TimestampUtc");
        MonitorStatus status = row.Status("Status");
        double? celsius = row.Number("TemperatureC");
        string type = row.Text("SensorType");
        string group = type switch
        {
            "Gpu" => ReportGroups.GpuTemperature,
            "AcpiThermalZone" => ReportGroups.ThermalZone,
            _ => throw new FormatException($"SensorType 不是有效值：{type}"),
        };
        string sensor = row.Text("SensorName");
        if (sensor.Length > 0)
        {
            builder.AddMetric(new SeriesKey(group, sensor, "°C", SeriesRole.Temperature, sensor), time, celsius, status);
        }
    }

    /// <summary>網路：各介面卡一列，同一次取樣（相同 SampleId，連續寫入）的 OK 介面卡相加為合計。</summary>
    private sealed class NetworkTotals(ReportBuilder builder)
    {
        private static readonly SeriesKey Download = new(ReportGroups.Network, "合計下載", "Mbps", SeriesRole.NetworkDownload, "下載");
        private static readonly SeriesKey Upload = new(ReportGroups.Network, "合計上傳", "Mbps", SeriesRole.NetworkUpload, "上傳");
        private string? _sampleId;
        private DateTimeOffset _time;
        private double _download;
        private double _upload;
        private bool _anyOk;
        private MonitorStatus _status;

        public void Add(CsvRow row)
        {
            DateTimeOffset time = row.Time("TimestampUtc");
            MonitorStatus status = row.Status("Status");
            double? download = row.Number("DownloadMbps");
            double? upload = row.Number("UploadMbps");
            string sampleId = row.Text("SampleId");
            string adapter = row.Text("AdapterName");

            if (!string.Equals(sampleId, _sampleId, StringComparison.Ordinal))
            {
                Flush();
                _sampleId = sampleId;
                _time = time;
                _status = status;
            }

            if (adapter.Length == 0)
            {
                return; // 狀態列：合計沿用該狀態
            }

            builder.AddMetric(new SeriesKey(ReportGroups.Network, $"{adapter} 下載", "Mbps"), time, download, status);
            builder.AddMetric(new SeriesKey(ReportGroups.Network, $"{adapter} 上傳", "Mbps"), time, upload, status);
            if (status == MonitorStatus.OK && download is double d && upload is double u)
            {
                _anyOk = true;
                _download += d;
                _upload += u;
            }
        }

        public void Flush()
        {
            if (_sampleId is null)
            {
                return;
            }

            MonitorStatus status = _anyOk ? MonitorStatus.OK : _status;
            builder.AddMetric(Download, _time, _anyOk ? _download : null, status);
            builder.AddMetric(Upload, _time, _anyOk ? _upload : null, status);
            _sampleId = null;
            _download = 0;
            _upload = 0;
            _anyOk = false;
        }
    }
}
