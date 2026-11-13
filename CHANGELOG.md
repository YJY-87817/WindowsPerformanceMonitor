# 變更紀錄

格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。資料格式（CSV schema）變更會特別標示。

## [Unreleased]

### Phase 2-7（2026-11-13）：網路

- 新增網路介面卡流量：Win32 `GetIfTable2`（不需 WMI）；篩選規則、速度計算、計數器重設、1.1 倍尖峰、合計與指定介面卡行為移植自舊版。
- GetIfTable2 欄位對應：篩選層與回送介面一律不列入；實體介面卡為 HardwareInterface＋ConnectorPresent 且無虛擬關鍵字。
- 啟動 Log 記錄網路篩選結果（納入與排除原因）。
- 主控台文字輸出加入網路合計與各介面卡；Smoke Test 檢查預設篩選與 `includeNames = ["*"]` 兩種情況及 NetworkMetrics CSV。

### Phase 2-6（2026-11-13）：NVIDIA GPU

- 新增 GPU 監控：nvidia-smi 一次查詢使用率、顯存與溫度；多張 GPU、名稱含逗號、不支援欄位留空、數值檢查；逾時或停止時結束程序。
- **與舊版不同**：nvidia-smi 只在 `C:\Windows\System32` 與 `%ProgramFiles%\NVIDIA Corporation\NVSMI` 尋找，不搜尋 PATH。
- GPU 溫度寫入 TemperatureMetrics（`SensorType = Gpu`）。
- 主控台文字輸出加入 GPU；Smoke Test 檢查 GPU（CI 沒有 GPU，預期 Unavailable）與 GpuMetrics CSV。

### Phase 2-5（2026-11-13）：磁碟 I/O

- 新增磁碟 I/O 監控：PDH 萬用字元計數器 `\LogicalDisk(*)`、`\PhysicalDisk(*)`（英文名稱加入）；讀寫 MB/s、IOPS、延遲、忙碌率；邏輯磁碟與磁碟容量模組相同，另列實體磁碟；暖機、單一磁碟異常不影響其他磁碟。
- **資料格式（發行前直接修改 v1）**：DiskIoMetrics 的 `Drive` 改為 `Scope`（Logical／Physical）＋`Disk`。schema 版本 +1 規則自第一次發行起適用。
- PDH 封裝加入萬用字元計數器讀取。
- 主控台文字輸出加入磁碟 I/O；Smoke Test 檢查磁碟 I/O 與 DiskIoMetrics CSV。

### Phase 2-4（2026-11-13）：程序

- 新增程序 RAM Top N：依 Working Set 排序取前 `process.topCount` 名；略過已結束或無法讀取的程序並計數；啟動時間只讀前 N 名，無權讀取時留空並計數。
- 主控台文字輸出加入前 3 名程序；Smoke Test 檢查程序與 ProcessMetrics CSV。

### Phase 2-3（2026-11-13）：磁碟容量

- 新增磁碟容量監控：`DriveInfo` 固定磁碟；總量、已使用、可用、使用率、可用百分比；單一磁碟異常不影響其他磁碟；磁碟可用空間告警自此生效。
- 主控台文字輸出加入磁碟；JSON 不再重複輸出 `devices`；Smoke Test 檢查磁碟與 DiskMetrics CSV。

### Phase 2-2（2026-11-13）：記憶體

- 新增記憶體監控：Win32 `GlobalMemoryStatusEx`（不需 WMI）；總量、已使用、可用（1024 進位 GB）、使用率；資料不合理時為 DataError。
- 主控台文字輸出加入記憶體；Smoke Test 檢查記憶體。

### Phase 2-1（2026-11-13）：CPU 與單次取樣流程

- 新增 CPU 監控：PDH `\Processor(_Total)\% Processor Time`，以英文名稱加入（不受語系影響）；暖機、0～100 檢查、失敗時下次重建查詢；不提供 WMI 備援。
- 新增 PDH 封裝（LibraryImport）與可替換介面。
- 新增主控台 `--once`（取樣一次並寫入 CSV／Log）與 `--once --json`；設定或目錄錯誤時不取樣並以結束碼 1 結束。
- 啟動 Log 加入資料來源說明。
- Smoke Test 改為實際取樣：檢查 CPU、SystemMetrics CSV 與 Application Log。
- 文件：記錄盤點 M2、M7、C8、S4 的確認結果。

### Phase 1c（2026-11-13）：告警

- 新增告警狀態機（NORMAL／PENDING／WARNING／COOLDOWN／RECOVERING／RECOVERED），持續、冷卻、遲滯、恢復時間與資料不可用時的行為沿用舊版規格 v1.5 §6。
- CPU、RAM、各固定磁碟獨立管理；磁碟支援百分比與 GB 條件（any／all）。
- 告警事件寫入 Application Log；提供進行中告警與最近 5 筆事件給 UI（`IAlertProvider`）。

### Phase 1b（2026-11-13）：CSV 與 Log

- **資料格式 v1 定稿**：7 種 CSV（SystemMetrics、DiskMetrics、DiskIoMetrics、GpuMetrics、NetworkMetrics、ProcessMetrics、TemperatureMetrics）。
- 新增各模組結果型別（資料契約），供 Phase 2 資料來源填入。
- 新增 CSV 寫入：UTF-8 BOM、CRLF、Invariant Culture、小數最多 3 位、空值不寫 0、狀態列；檔名含 schema 版本與本機日期；檔案被鎖定時 200 ms 重試，仍失敗累計遺失列數並寫 Error Log。
- 新增專用 Log 服務：Application／Error Log 每日分檔；Error Log 去重與 10 分鐘彙總、恢復紀錄；LoopOverrun 紀錄；啟動與停止 Log。
- 新增啟動前目錄可寫入檢查。

### Phase 1a（2026-11-13）：資料模型、設定、排程

- 新增資料模型：`SampleId`、`ModuleResult`、`MonitorSnapshot`；多裝置整體狀態規則 `StatusAggregator`；錯誤訊息整理（第一行、最長 200 字）。
- 新增監控介面：`IMonitor`、`IHostInfo`、`ISnapshotProvider`、`ISnapshotRecorder`；`IClock` 加入 `DelayAsync`。
- 新增設定：`AppSettings`、`SettingsParser`（型別檢查、缺少欄位用預設值、未知欄位警告）、`SettingsValidator`（範圍與相互關係，一次列出所有錯誤）、`SettingsFileLoader`、`AppDataPaths`；範例 `docs/settings.example.json`。
- 新增排程與主迴圈：`IntervalSchedule`（略過錯過的格點、不補跑）、`SampleRunner`（模組失敗隔離）、`MonitoringEngine`（暖機、取樣、CSV／程序紀錄格點、停止時補寫）、`SnapshotStore`。
- 文件：記錄盤點與資料格式的確認結果；規格 v2.0 新增取樣流程與設定檔章節；決策 0005 補上目錄寫不進去的處理。

### Phase 0（2026-11-13）

- 新增 Solution 骨架：Core、Application、Reporting、Infrastructure、App（WPF）、Cli 與 4 個測試專案。
- 新增共用建置設定：Nullable、分析器、警告視為錯誤、集中管理套件版本、固定 SDK 版本。
- 新增 `MonitorStatus`、`ErrorCode`（沿用舊版規格 v1.5）、`IClock` 與 `SystemClock`。
- 新增 Cli `--self-check`、`--version`、`--help`。
- 新增 CI：7 項檢查（Lint、Format、Type Check、Unit Test、Build Check、Git Diff Check、Smoke Test）。
- 新增文件：規格書 v2.0 初稿、架構、舊版盤點、資料格式 v1 初稿、硬體相容性、測試規劃、技術決策 0001～0006。
