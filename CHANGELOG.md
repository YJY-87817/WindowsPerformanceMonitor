# 變更紀錄

格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。資料格式（CSV schema）變更會特別標示。

## [Unreleased]

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
