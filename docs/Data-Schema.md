# 資料格式（schema v1 初稿）

- 版本：**v1**（2026-11-13，Phase 1b 實作）。欄位已由程式 `CsvTables` 實作並以測試固定
- 版本 +1 規則自**第一次發行**起適用；發行前（Phase 2 開發中）欄位調整直接修改 v1，並記錄在下方修訂紀錄
- 相關決策：[0005 資料目錄、檔案命名與 schema 版本](decisions/0005-data-storage.md)

## 1. 檔案

預設位置 `%LOCALAPPDATA%\WindowsPerformanceMonitor\`：

```text
Data/
├── SystemMetrics_v1_YYYYMMDD.csv
├── DiskMetrics_v1_YYYYMMDD.csv          ← 磁碟容量
├── DiskIoMetrics_v1_YYYYMMDD.csv        ← 磁碟 I/O
├── GpuMetrics_v1_YYYYMMDD.csv
├── NetworkMetrics_v1_YYYYMMDD.csv
├── ProcessMetrics_v1_YYYYMMDD.csv
└── TemperatureMetrics_v1_YYYYMMDD.csv   ← GPU 與 ACPI 熱區
Logs/
├── Application_YYYYMMDD.log
└── Error_YYYYMMDD.log
```

- `YYYYMMDD` 是紀錄時間的本機日期；跨午夜寫入新檔，不覆寫前一天。
- `v1` 是該檔的 schema 版本。欄位增減、改名或改意義時版本 +1，寫到新檔；舊檔不修改。每個類型的版本各自獨立。
- 監控資料與 Log 可能揭露使用者使用的軟體，不進版控、不自動上傳。

## 2. 共通格式（沿用舊規格 v1.5 §5.3）

| 項目 | 規則 |
|---|---|
| 編碼 | UTF-8 BOM（Excel 正確顯示中文） |
| 換行 | CRLF |
| 時間 | `TimestampUtc`：`yyyy-MM-ddTHH:mm:ss.fffZ` |
| 數值 | Invariant Culture；小數最多 3 位、省略尾端 0；不用科學記號 |
| Boolean | `True`／`False` |
| 空值 | 無法取得的數值寫空欄位，以 `Status` 說明；不得寫 0 |
| 文字 | 依 CSV 規則跳脫逗號、雙引號、換行 |
| 關聯 | 同一次取樣的所有資料使用相同 `SampleId`（32 位十六進位） |
| 狀態列 | 分項檔（磁碟、磁碟 I/O、GPU、網路、程序、溫度）的模組有結果但沒有任何項目時（例如 `Unavailable`、`Disabled`），寫一筆只有 `SampleId`、`TimestampUtc`、`Status`、`ErrorCode`（若該檔有此欄）的列，其他欄位留空；快照中沒有該模組時不寫 |
| 狀態值 | `OK`、`WarmingUp`、`Unavailable`、`Error`、`Disabled` |
| ErrorCode | `Unavailable`、`ConfigurationError`、`Timeout`、`DataError`、`AccessDenied`、`IOError`、`TransientError`、`InternalError` |

容量以 1024 進位（`1 GB = 1,073,741,824 bytes`，`1 MB = 1,048,576 bytes`）；網路速度以十進位（`Mbps = bytes × 8 ÷ 秒 ÷ 1,000,000`）。

## 3. 各檔欄位

### 3.1 SystemMetrics_v1（每次 CSV 紀錄一筆）

| 欄位 | 型態 | 說明 |
|---|---|---|
| SampleId | String | |
| TimestampUtc | DateTime | |
| TimestampLocal | DateTime | 含時區偏移的本機時間（`yyyy-MM-ddTHH:mm:ss.fffzzz`）；只有本檔有此欄位 |
| ComputerName | String | |
| CpuUsagePercent | Decimal | |
| CpuStatus | Status | |
| MemoryTotalGB | Decimal | |
| MemoryUsedGB | Decimal | |
| MemoryFreeGB | Decimal | |
| MemoryUsagePercent | Decimal | |
| MemoryStatus | Status | |
| DiskStatus | Status | 磁碟容量模組整體狀態 |
| DiskIoStatus | Status | 磁碟 I/O 模組整體狀態 |
| GpuStatus | Status | |
| NetworkStatus | Status | |
| ProcessStatus | Status | |
| TemperatureStatus | Status | |
| SampleDurationMs | Decimal | 該次取樣總耗時 |
| LoopOverrun | Boolean | 是否超過排程週期 |

### 3.2 DiskMetrics_v1（每個固定磁碟一筆）

`SampleId`、`TimestampUtc`、`Drive`（例如 `C:`）、`VolumeLabel`、`TotalGB`、`UsedGB`、`FreeGB`、`UsedPercent`、`FreePercent`、`Status`、`ErrorCode`

### 3.3 DiskIoMetrics_v1（每個邏輯磁碟與實體磁碟各一筆）

| 欄位 | 型態 | 來源（`\LogicalDisk(*)\…` 或 `\PhysicalDisk(*)\…`） |
|---|---|---|
| SampleId、TimestampUtc | | |
| Scope | Text | `Logical`（邏輯磁碟）或 `Physical`（實體磁碟） |
| Disk | Text | 邏輯磁碟為代號（`C:`）；實體磁碟為 Windows 名稱（`0 C: D:`） |
| ReadMBps | Decimal | `Disk Read Bytes/sec` ÷ 1,048,576 |
| WriteMBps | Decimal | `Disk Write Bytes/sec` ÷ 1,048,576 |
| ReadIOPS | Decimal | `Disk Reads/sec` |
| WriteIOPS | Decimal | `Disk Writes/sec` |
| ReadLatencyMs | Decimal | `Avg. Disk sec/Read` × 1000 |
| WriteLatencyMs | Decimal | `Avg. Disk sec/Write` × 1000 |
| BusyPercent | Decimal | 100 − `% Idle Time`，限制在 0～100 |
| Status、ErrorCode | | 第一次讀取為 `WarmingUp` |

延遲是計數器層級的平均服務時間，不等同應用程式端到端延遲。邏輯與實體磁碟是同一份 I/O 的兩種角度，彙總時須以 `Scope` 分開，不可相加。

### 3.4 GpuMetrics_v1（每張 NVIDIA GPU 一筆）

`SampleId`、`TimestampUtc`、`GpuIndex`、`GpuName`、`UsagePercent`、`MemoryUsedMB`、`MemoryTotalMB`、`Status`、`ErrorCode`

GPU 溫度寫在 TemperatureMetrics（`SensorType = Gpu`，`SensorName` 為「GPU 〈index〉 〈名稱〉」，狀態取該 GPU 的狀態）。

### 3.5 NetworkMetrics_v1（每張納入合計的介面卡一筆）

`SampleId`、`TimestampUtc`、`AdapterName`、`InterfaceDescription`、`InterfaceId`、`IsPhysical`、`ReceivedBytes`、`SentBytes`、`DownloadMbps`、`UploadMbps`、`Status`、`ErrorCode`

### 3.6 ProcessMetrics_v1（每個 Top N 程序一筆）

`SampleId`、`TimestampUtc`、`Rank`、`ProcessName`、`Pid`、`WorkingSetMB`、`StartTimeUtc`、`Status`

只在程序紀錄到期時寫入（見規格 v2.0 §3.3）。`Status` 為程序模組狀態；部分程序讀取失敗仍為 `OK`。

### 3.7 TemperatureMetrics_v1（每個感測器一筆）

| 欄位 | 型態 | 說明 |
|---|---|---|
| SampleId、TimestampUtc | | |
| SensorType | String | `Gpu` 或 `AcpiThermalZone` |
| SensorName | String | GPU 名稱（含 index）或熱區名稱 |
| TemperatureC | Decimal | 攝氏 |
| Status、ErrorCode | | |

`AcpiThermalZone` 不是 CPU 核心溫度，畫面與報告都不得如此標示。

## 4. Log 格式（沿用舊規格 v1.5 §5.6）

一行一筆：`yyyy-MM-ddTHH:mm:ss.fffzzz [INFO|WARN|ERROR] [來源] 訊息`；UTF-8 BOM；每日分檔；訊息內換行轉為空白。告警事件寫入 Application Log。

## 5. 與舊版的差異

| 項目 | 舊版 V1.0 | 新版 v1 |
|---|---|---|
| 檔名 | `<類型>_YYYYMMDD.csv` | `<類型>_v1_YYYYMMDD.csv` |
| 標題列不符 | 改寫 `_2` 檔 | 不需要（版本在檔名） |
| 磁碟 | 只有容量 | 容量與 I/O 分開兩檔 |
| 溫度 | 無 | TemperatureMetrics |
| ErrorCode 欄位 | 無（只在 Log） | 分項檔加 `ErrorCode` |
| 欄位大小寫 | `GPUIndex`、`PID` | `GpuIndex`、`Pid`（.NET 命名慣例） |
