# 舊版功能盤點與移植規劃

- 版本：Phase 0 初稿（2026-11-13）
- 舊版依據：[`Specification_v1.5.md`](https://github.com/YJY-87817/Windows_PC_Performance_Monitor_Specification/blob/main/docs/Specification_v1.5.md)（下稱「v1.5」）與 PowerShell V1.0 實作
- 處置分類：**保留**（行為照舊）、**改善**（保留需求、改做法）、**不再需要**、**待確認**

> **確認結果（2026-11-13）：** 負責人確認本表所有建議處置照建議辦理。標示「待確認」的項目在對應 Phase 開工前另行提問，確認後更新本表。

## 1. 監控模組

| # | 舊版功能 | v1.5 | 舊版做法 | 建議處置 | C# 做法 | Phase |
|---|---|---|---|---|---|---|
| M1 | CPU 使用率 | §3.2 | PDH 計數器（索引查詢在地化名稱）＋ CIM 備援 | 改善 | `PdhAddEnglishCounter` 加入 `\Processor(_Total)\% Processor Time`，不受語系影響 | 2 |
| M2 | CPU 的 CIM 備援（`Win32_Processor.LoadPercentage`） | §3.2.1 | 計數器失敗時改用 CIM | 不再需要（已確認） | 改用英文計數器後，語系不再是失敗原因；少一個 `System.Management` 依賴。失敗時回報 `Error` | 2 |
| M3 | 記憶體（總量、已用、可用、使用率） | §3.3 | CIM `Win32_OperatingSystem` | 改善 | Win32 `GlobalMemoryStatusEx`（不需 WMI） | 2 |
| M4 | 磁碟容量（固定磁碟） | §3.4 | CIM `Win32_LogicalDisk`（DriveType 3） | 改善 | `System.IO.DriveInfo`（`DriveType.Fixed`） | 2 |
| M5 | 磁碟 I/O | — | 舊版無 | 新增（已確認；Phase 2-5 確認加入實體磁碟） | PDH `\LogicalDisk(*)\…`、`\PhysicalDisk(*)\…`；見規格 v2.0 §4.5 | 2 |
| M6 | NVIDIA GPU 使用率、顯存 | §3.5 | `nvidia-smi`，逾時 Kill | 保留＋新增溫度（已確認） | `Process` 執行 `nvidia-smi`，一次查詢含 `temperature.gpu`；解析規則沿用 v1.4 決策；尋找改為只找 System32 與 `%ProgramFiles%\NVIDIA Corporation\NVSMI`，不搜尋 PATH（Phase 2-6 確認） | 2 |
| M7 | 網路介面卡流量 | §3.6 | CIM `MSFT_NetAdapter*` | 保留，改用 Win32 `GetIfTable2`（已確認） | 有「是否硬體介面卡」旗標與連線速度，篩選結果最接近舊版。欄位對應（Phase 2-7 確認）：Hidden → 篩選層／回送介面一律不列入；實體 → HardwareInterface＋ConnectorPresent＋無虛擬關鍵字；見規格 v2.0 §4.7 | 2 |
| M8 | 網路篩選規則、合計、速度尖峰 1.1 倍檢查 | §3.6.1 | 見 v1.4 決策 | 保留 | 規則移植為純函式並沿用舊版測試案例 | 2 |
| M9 | 程序 RAM Top N | §3.7 | `Get-Process`，WorkingSet64 | 保留 | `Process.GetProcesses()`；讀取失敗略過並計數 | 2 |
| M10 | ACPI 熱區溫度 | — | 舊版無 | 新增（已確認） | PDH `\Thermal Zone Information(*)\Temperature`（K → °C） | 2 |

## 2. 共通行為

| # | 舊版功能 | v1.5 | 建議處置 | 說明 | Phase |
|---|---|---|---|---|---|
| C1 | 5 種狀態、8 種 ErrorCode、多裝置整體狀態規則 | §3.1.1、§10.1 | 保留（已確認） | 已建立 `MonitorStatus`、`ErrorCode` | 0／1 |
| C2 | 取樣 2 秒、畫面 2 秒、CSV 10 秒，三者分開 | §3.1 | 保留（已確認） | | 1 |
| C3 | 單調時間格點排程、超時略過不補跑、`LoopOverrun` | §4.3 | 保留 | | 1 |
| C4 | `SampleId` 關聯同一次取樣 | §3.1 | 保留（已確認） | | 1 |
| C5 | 啟動暖機 1 秒（CPU、網路），第一個畫面就有數值 | §3.6 | 保留 | 磁碟 I/O、熱區也一起暖機 | 2 |
| C6 | 模組失敗隔離、有限重試 | §10.2、§10.3 | 保留 | | 1 |
| C7 | 停止時補寫最後一筆完整快照、釋放資源、寫停止 Log | §4.4 | 保留 | 觸發方式改為 WPF 停止／關閉視窗、Cli Ctrl+C | 1 |
| C8 | CIM 查詢逾時（`timeouts.cimSeconds`） | §3.1 | 不再需要（已確認） | M2、M7 都不用 WMI，不提供此設定 | 2 |

## 3. 告警

| # | 舊版功能 | v1.5 | 建議處置 | 說明 | Phase |
|---|---|---|---|---|---|
| A1 | CPU、RAM、磁碟可用空間告警 | §6.1 | 保留（已確認） | | 1／2 |
| A2 | 狀態機（PENDING／WARNING／COOLDOWN／RECOVERING／RECOVERED）、持續、冷卻、遲滯 | §6.2 | 保留（已確認） | 移植為純邏輯並沿用舊版測試案例 | 1 |
| A3 | 磁碟條件 any／all（百分比＋GB） | §6.3 | 保留 | | 1 |
| A4 | 資料不可用時的告警行為 | §6.4 | 保留 | | 1 |
| A5 | 告警事件寫入 Application Log（不另建 CSV） | §6.4 | 保留 | 報告從 Log 讀取告警事件 | 1 |
| A6 | 畫面 ALERTS 區（紅色、最近 5 筆） | §6.4 | 改善 | 改為 WPF 總覽與狀態頁 | 3 |

## 4. 資料與 Log

| # | 舊版功能 | v1.5 | 建議處置 | 說明 | Phase |
|---|---|---|---|---|---|
| D1 | 五份 CSV | §5.4 | 改善 | 新 schema：磁碟容量與 I/O 分開，新增溫度；見 [Data-Schema.md](Data-Schema.md) | 1／2 |
| D2 | `TimestampUtc` ＋ `TimestampLocal` | §5.3 | 改善（已確認） | SystemMetrics 保留兩者（Excel 直接看本機時間），分項檔只放 `TimestampUtc` | 1 |
| D3 | UTF-8 BOM、CRLF、Invariant Culture、小數最多 3 位、空值不寫 0 | §5.3 | 保留 | BOM 讓 Excel 正確顯示中文 | 1 |
| D4 | 每日分檔（本機日期） | §5.1 | 保留（已確認） | 檔名加 schema 版本，見決策 0005 | 1 |
| D5 | 標題列不符改寫 `_2` 檔 | §5.5 | 不再需要（已確認） | 由檔名 schema 版本取代 | — |
| D6 | 每次寫入開檔即關、鎖定時 200 ms 重試、累計遺失筆數 | §5.5 | 保留 | | 1 |
| D7 | 程序紀錄獨立開關與間隔 | §5.2 | 保留 | | 1 |
| D8 | 資料目錄：程式旁 → 寫不進去改 `%LOCALAPPDATA%` | §5.1 | 改善（已確認） | 預設 `%LOCALAPPDATA%\WindowsPerformanceMonitor`；設定的目錄寫不進去時不開始監控並提示，不自動改寫到其他位置。見決策 0005 | 1 |
| D9 | Application／Error Log、格式、去重、10 分鐘彙總 | §5.6 | 保留 | | 1 |
| D10 | 啟動 Log：版本、設定摘要、CPU 來源、網路篩選結果 | §5.6 | 保留（已完成） | 各模組 `SourceDescription`；網路列出納入與排除原因 | 1／2 |

## 5. 設定、介面與工具

| # | 舊版功能 | v1.5 | 建議處置 | 說明 | Phase |
|---|---|---|---|---|---|
| S1 | JSON 設定檔與完整驗證 | §9 | 保留（已確認：沿用舊版並精簡） | 位置 `%LOCALAPPDATA%\WindowsPerformanceMonitor\settings.json`；WPF 設定頁讀寫同一檔。欄位見規格 v2.0 §7 | 1／3 |
| S2 | 命令列即時畫面 | §4.1 | 不再需要 | 由 WPF 取代；Cli 只提供診斷命令 | — |
| S3 | `-Once` | §5.2 | 改善（已完成） | Cli `--once`，行為與連續模式相同（寫 CSV 與 Log） | 2 |
| S4 | `-PassThru`（輸出物件供腳本使用） | — | 改善（已確認、已完成） | Cli `--once --json` 輸出 JSON | 2 |
| S5 | `-Verbose` 診斷輸出 | — | 改善 | 改用 Logging 等級設定 | 1 |
| S6 | 24 小時穩定性工具 `Test-Stability.ps1` | §7.2 | 改善 | Phase 5 提供 C# 版或以 Cli 搭配外部量測 | 5 |
| S7 | 隱私：不上傳、不記錄機密、只執行 nvidia-smi | §7.4 | 保留 | | 全部 |

## 6. 待確認事項彙整

| # | 事項 | 建議 | 何時確認 |
|---|---|---|---|
