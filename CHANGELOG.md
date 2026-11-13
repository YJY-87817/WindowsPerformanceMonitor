# 變更紀錄

格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。資料格式（CSV schema）變更會特別標示。

## [Unreleased]

### Phase 0（2026-11-13）

- 新增 Solution 骨架：Core、Application、Reporting、Infrastructure、App（WPF）、Cli 與 4 個測試專案。
- 新增共用建置設定：Nullable、分析器、警告視為錯誤、集中管理套件版本、固定 SDK 版本。
- 新增 `MonitorStatus`、`ErrorCode`（沿用舊版規格 v1.5）、`IClock` 與 `SystemClock`。
- 新增 Cli `--self-check`、`--version`、`--help`。
- 新增 CI：7 項檢查（Lint、Format、Type Check、Unit Test、Build Check、Git Diff Check、Smoke Test）。
- 新增文件：規格書 v2.0 初稿、架構、舊版盤點、資料格式 v1 初稿、硬體相容性、測試規劃、技術決策 0001～0006。
