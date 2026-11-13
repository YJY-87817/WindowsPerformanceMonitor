# 變更紀錄

格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。資料格式（CSV schema）變更會特別標示。

## [Unreleased]

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
