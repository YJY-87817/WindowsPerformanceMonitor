# Windows 個人電腦效能監控工具 V2.0 規格書

- 狀態：**初稿**（Phase 0，2026-11-13）；各 Phase 開工前確認的事項會陸續寫入
- 前一版：[舊 repo `Specification_v1.5.md`](https://github.com/YJY-87817/Windows_PC_Performance_Monitor_Specification/blob/main/docs/Specification_v1.5.md)（PowerShell V1.0，下稱「v1.5」）
- 開發規劃：[舊 repo `Phase4-Plan-v2.1.md`](https://github.com/YJY-87817/Windows_PC_Performance_Monitor_Specification/blob/main/docs/plans/Phase4-Plan-v2.1.md)
- 附屬文件：[Architecture](Architecture.md)、[Migration-Plan](Migration-Plan.md)、[Data-Schema](Data-Schema.md)、[Hardware-Compatibility](Hardware-Compatibility.md)、[Testing-Plan](Testing-Plan.md)、[技術決策](decisions/README.md)

本規格以 v1.5 為底。標示「沿用 v1.5 §x」的項目，行為與該章節相同，細節以 v1.5 為準；新版不同之處直接寫在本文件。

## 修訂紀錄

| 版本 | 日期 | 狀態 | 內容 |
|---|---|---|---|
| 2.0 初稿 | 2026-11-13 | 初稿 | 建立文件：範圍、共通規格、資料、告警、平台；依 Phase 0 盤點結果整理 |

---

## 1. 專案概述

### 1.1 目標

以 C# + .NET 10 + WPF 重寫舊版 PowerShell 工具，提供：

1. 即時監控：CPU、記憶體、磁碟容量、磁碟 I/O、NVIDIA GPU（使用率、顯存、溫度）、網路介面卡流量、程序 RAM Top N、ACPI 熱區溫度。
2. 告警：CPU、RAM、磁碟可用空間。
3. 歷史紀錄：CSV 與 Log。
4. WPF 桌面介面。
5. 歷史分析：離線 HTML 報告。

### 1.2 開發原則

1. 一般使用者權限即可執行；需要提高權限的資料不列為必要功能。
2. 不連網、不上傳資料；除 Windows 與硬體原廠驅動／工具外不要求另外安裝軟體。
3. 監控模組彼此隔離，單一模組失敗不影響其他模組。
4. 無法取得的數值不得以 0 代替。
5. 資料格式版本化。

### 1.3 不在範圍

Windows 桌面通知、SMART 健康資訊、AMD／Intel GPU、CPU 核心溫度、舊版 CSV 匯入、雲端同步、Windows Service、自動開機與系統匣常駐（待 UI 需求確認）。

## 2. 開發階段

| Phase | 內容 |
|---|---|
| 0 | 盤點、架構、Solution 骨架、CI（本文件初稿） |
| 1 | 監控核心：資料模型、排程、狀態、CSV／Log、告警狀態機、設定 |
| 2 | 各資料來源（每個一個 PR）、Cli `--once` |
| 3 | WPF 介面 |
| 4 | 歷史分析與 HTML 報告 |
| 5 | 整合測試、發行與文件 |

## 3. 共通監控規格

| 項目 | 規格 |
|---|---|
| 預設週期 | 取樣 2 秒、畫面 2 秒、CSV 10 秒，三者分開設定（沿用 v1.5 §3.1） |
| 時間 | 時間戳使用系統時間（UTC）；經過時間使用單調計時 |
| 資料關聯 | 每次完整取樣一個 `SampleId` |
| 排程 | 單調時間格點；超時略過錯過的格點、不補跑；不重疊執行；記錄 `LoopOverrun`（沿用 v1.5 §4.3） |
| 暖機 | 啟動時速率型資料來源（CPU、磁碟 I/O、網路）建立基準並等待 1 秒 |
| 失敗處理 | 模組回傳狀態，不中止其他模組；重試有限次數（沿用 v1.5 §10） |
| 停止 | 取消背景工作、補寫最後一筆完整快照、釋放資源、寫停止 Log（沿用 v1.5 §4.4） |

### 3.1 狀態與錯誤分類

沿用 v1.5 §3.1.1 與 §10.1：

- 狀態：`OK`、`WarmingUp`、`Unavailable`、`Error`、`Disabled`。
- ErrorCode：`Unavailable`、`ConfigurationError`、`Timeout`、`DataError`、`AccessDenied`、`IOError`、`TransientError`、`InternalError`。
- 多裝置整體狀態：沒有裝置 → `Unavailable`；任一 `Error` → `Error`；其餘全 `Unavailable` → `Unavailable`；排除 `Unavailable` 後全 `WarmingUp` → `WarmingUp`；其餘 → `OK`。

### 3.2 單位

容量 1024 進位（GB、MB）；網路速度十進位 Mbps；百分比 0～100，超出範圍視為資料異常。

## 4. 監控模組

各模組的資料來源與無法取得時的行為見 [Hardware-Compatibility](Hardware-Compatibility.md)；移植自舊版的行為見 [Migration-Plan](Migration-Plan.md)。各模組的詳細規格在 Phase 2 該模組開工時補入本章。

| 模組 | 主要數值 | 舊版對應 |
|---|---|---|
| CPU | 總使用率 | v1.5 §3.2 |
| 記憶體 | 總量、已用、可用、使用率 | v1.5 §3.3 |
| 磁碟容量 | 每個固定磁碟的總量、已用、可用、使用率 | v1.5 §3.4 |
| 磁碟 I/O | 每個邏輯磁碟的讀寫 MB/s、IOPS、延遲、忙碌率 | 新增 |
| GPU | 每張 NVIDIA GPU 的使用率、顯存、溫度 | v1.5 §3.5（溫度新增） |
| 網路 | 每張介面卡的下載／上傳 Mbps、合計 | v1.5 §3.6 |
| 程序 | RAM Top N（預設 5，範圍 1～20） | v1.5 §3.7 |
| ACPI 熱區 | 每個熱區溫度（°C） | 新增 |

## 5. 資料與 Log

見 [Data-Schema](Data-Schema.md) 與 [決策 0005](decisions/0005-data-storage.md)。重點：

- 位置：`%LOCALAPPDATA%\WindowsPerformanceMonitor\Data`、`\Logs`，可設定。
- 檔名：`<類型>_v<版本>_<本機日期 YYYYMMDD>.csv`。
- 格式：UTF-8 BOM、CRLF、Invariant Culture、空值不寫 0。
- 鎖定處理：每次寫入開檔即關；鎖定時 200 ms 後重試，仍失敗則略過並累計遺失筆數（沿用 v1.5 §5.5）。
- Log：Application／Error，去重與 10 分鐘彙總（沿用 v1.5 §5.6）。

## 6. 告警

沿用 v1.5 §6：門檻、持續時間、冷卻、遲滯、狀態機（NORMAL／PENDING／WARNING／COOLDOWN／RECOVERING／RECOVERED）、磁碟 any／all 條件、資料不可用時的行為；告警事件寫入 Application Log。呈現方式改為 WPF（Phase 3）。

## 7. 設定

沿用 v1.5 §9 的 JSON 設定概念與驗證規則；檔案位置改為 `%LOCALAPPDATA%\WindowsPerformanceMonitor\settings.json`，WPF 設定頁讀寫同一檔。欄位定稿於 Phase 1。

## 8. 介面

| 介面 | 說明 | Phase |
|---|---|---|
| WPF | 總覽、即時監控、磁碟、網路、溫度、程序、設定、狀態／錯誤 | 3 |
| Cli | `--self-check`、`--version`、`--help`；`--once` | 0；2 |

## 9. 非功能需求

| 項目 | 規格 |
|---|---|
| 平台 | Windows 11（主要）、Windows 10 22H2（相容性測試）；win-x64 |
| 發行 | self-contained（[決策 0006](decisions/0006-self-contained-publish.md)） |
| 權限 | 一般使用者 |
| UI | 採樣與寫檔不得阻塞 UI Thread |
| 穩定性 | 連續執行 24 小時無非預期終止、無持續資源成長（量測方式沿用 v1.5 §7.2） |
| 隱私 | 沿用 v1.5 §7.4 |
| 依賴 | 依 [決策 0003](decisions/0003-dependencies.md) |

## 10. 驗收

各 Phase 的完成標準見開發規劃 v2.1；測試方式與紀錄見 [Testing-Plan](Testing-Plan.md)。

## 11. 待確認事項

| # | 事項 | 建議 | 何時確認 |
|---|---|---|---|
| 1 | CPU 的 CIM 備援是否移植（盤點 M2） | 不移植 | Phase 2 |
| 2 | 網路資料來源（盤點 M7） | Win32 `GetIfTable2` | Phase 2 |
| 3 | CIM 逾時設定（盤點 C8） | 若不用 WMI 則移除 | Phase 2 |
| 4 | 本機時間欄位（盤點 D2） | 只在 SystemMetrics 保留 | Phase 1 |
| 5 | Cli JSON 輸出（盤點 S4） | `--once --json` | Phase 2 |
| 6 | 資料目錄無法寫入時的處理 | Phase 1 提出建議 | Phase 1 |
| 7 | 程式碼簽章、ARM64 | Phase 5 評估 | Phase 5 |
