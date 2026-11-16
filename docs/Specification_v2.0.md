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
| 2.0 初稿 | 2026-11-16 | Phase 3c | §8.1 設定頁（全部欄位、儲存並重新啟動、.bak 備份）；§7.1 設定檔寫入 |
| 2.0 初稿 | 2026-11-13 | Phase 3b | §8.1 即時趨勢（最近 5 分鐘、斷開規則）、磁碟、網路、溫度、程序頁 |
| 2.0 初稿 | 2026-11-13 | Phase 3a | §8.1 WPF：開啟即監控、關閉即結束、不做系統匣、總覽與狀態頁（決策 0007） |
| 2.0 初稿 | 2026-11-13 | Phase 2-8 | §4.8 ACPI 熱區（優先高精度計數器；不偵測固定值） |
| 2.0 初稿 | 2026-11-13 | Phase 2-7 | §4.7 網路（GetIfTable2 欄位對應） |
| 2.0 初稿 | 2026-11-13 | Phase 2-6 | §4.6 NVIDIA GPU；nvidia-smi 只在固定位置尋找（不搜尋 PATH） |
| 2.0 初稿 | 2026-11-13 | Phase 2-5 | §4.5 磁碟 I/O（邏輯磁碟加實體磁碟） |
| 2.0 初稿 | 2026-11-13 | Phase 2-4 | §4.4 程序 |
| 2.0 初稿 | 2026-11-13 | Phase 2-3 | §4.3 磁碟容量 |
| 2.0 初稿 | 2026-11-13 | Phase 2-2 | §4.2 記憶體 |
| 2.0 初稿 | 2026-11-13 | Phase 2-1 | 盤點 M2、M7、C8、S4 確認；§4.1 CPU；§8 Cli `--once`、`--json` |
| 2.0 初稿 | 2026-11-13 | Phase 1c | §6 補上告警實作規則 |
| 2.0 初稿 | 2026-11-13 | Phase 1b | §5 補上 CSV 寫入與 Log 的實作規則；Data-Schema 定為 v1 |
| 2.0 初稿 | 2026-11-13 | Phase 1a | 盤點建議全部確認；新增 §3.3 取樣流程、§7 設定檔欄位與驗證規則；本機時間欄位只在 SystemMetrics；資料目錄寫不進去時不開始監控 |

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

Windows 桌面通知、SMART 健康資訊、AMD／Intel GPU、CPU 核心溫度、舊版 CSV 匯入、雲端同步、Windows Service、自動開機與系統匣常駐（Phase 3 確認 V2.0 不做）、深色主題（V2.0 只做淺色）。

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

### 3.3 取樣流程（Phase 1a）

1. **暖機：** 對每個啟用的模組呼叫暖機（建立速率基準），暖機失敗不影響啟動；接著等待 1 秒。
2. **取樣：** 依取樣格點依序執行所有模組，同一時間只有一輪取樣。停用的模組結果為 `Disabled`、不呼叫；模組擲出未預期例外時，該模組為 `Error`／`InternalError`，其他模組照常取樣。每個模組與整輪的耗時都記錄下來；整輪耗時超過取樣週期時標示 `LoopOverrun`。
3. **格點：** 取樣、CSV、程序紀錄三個格點從同一起點開始。超時後跳到下一個尚未經過的格點，錯過的格點不補跑。
4. **CSV：** 第一次取樣完成後立即寫入，之後每 `logSeconds` 寫入當時最新的快照；同一快照不重複寫入。程序紀錄只在 CSV 紀錄時點檢查是否到期（間隔小於 `logSeconds` 時等同 `logSeconds`）。`logging.enabled = false` 時不寫 CSV。
5. **發布：** 每次取樣完成後發布最新快照；畫面依自己的更新週期讀取，與取樣週期分開。
6. **停止：** 捨棄進行中的不完整取樣；最後一筆已完成但尚未寫入的快照補寫一次。

## 4. 監控模組

各模組的資料來源與無法取得時的行為見 [Hardware-Compatibility](Hardware-Compatibility.md)；移植自舊版的行為見 [Migration-Plan](Migration-Plan.md)。各模組的詳細規格在 Phase 2 該模組開工時補入本章。

| 模組 | 主要數值 | 舊版對應 |
|---|---|---|
| CPU | 總使用率 | v1.5 §3.2 |
| 記憶體 | 總量、已用、可用、使用率 | v1.5 §3.3 |
| 磁碟容量 | 每個固定磁碟的總量、已用、可用、使用率 | v1.5 §3.4 |
| 磁碟 I/O | 每個邏輯磁碟與實體磁碟的讀寫 MB/s、IOPS、延遲、忙碌率 | 新增 |
| GPU | 每張 NVIDIA GPU 的使用率、顯存、溫度 | v1.5 §3.5（溫度新增） |
| 網路 | 每張介面卡的下載／上傳 Mbps、合計 | v1.5 §3.6 |
| 程序 | RAM Top N（預設 5，範圍 1～20） | v1.5 §3.7 |
| ACPI 熱區 | 每個熱區溫度（°C） | 新增 |

### 4.1 CPU（Phase 2-1）

| 項目 | 規格 |
|---|---|
| 來源 | PDH `\Processor(_Total)\% Processor Time`，以 `PdhAddEnglishCounter` 加入（不受 Windows 語系影響） |
| 暖機 | 啟動時收集一次建立基準；第一次讀取若尚無基準回報 `WarmingUp` |
| 有效值 | 0～100；超出範圍或非數字為 `Error`／`DataError` |
| 失敗 | 讀取失敗為 `Error`／`DataError`（附 PDH 錯誤碼）；PDH 呼叫失敗為 `Error`／`TransientError`，下次取樣重新建立查詢並重新暖機 |
| 無 PDH | 無法載入 pdh.dll（非 Windows）為 `Unavailable` |
| 備援 | 不提供 WMI 備援（盤點 M2） |
| 啟動 Log | 記錄 CPU 來源 |

### 4.2 記憶體（Phase 2-2）

| 項目 | 規格 |
|---|---|
| 來源 | Win32 `GlobalMemoryStatusEx`（不需 WMI） |
| 數值 | 總量 `ullTotalPhys`；可用 `ullAvailPhys`（含待命清單，與工作管理員「可用」一致）；已使用 = 總量 − 可用；使用率 = 已使用 ÷ 總量 × 100；GB 為 1024 進位 |
| 異常 | 總量為 0、可用大於總量為 `Error`／`DataError`（不除以 0）；API 失敗為 `Error`／`TransientError` |
| 說明 | 記憶體使用率與程序 Working Set 加總不保證相等（快取、共享記憶體、核心使用量） |

### 4.3 磁碟容量（Phase 2-3）

| 項目 | 規格 |
|---|---|
| 來源 | `System.IO.DriveInfo`，只列 `DriveType.Fixed`（網路與卸除式磁碟不在範圍） |
| 識別 | 磁碟代號（例如 `C:`）；標籤只作顯示；依代號排序 |
| 數值 | 總量 `TotalSize`；可用 `TotalFreeSpace`（不受使用者配額限制，同舊版 `FreeSpace`）；已使用 = 總量 − 可用；使用率、可用百分比；GB 為 1024 進位 |
| 異常 | 單一磁碟容量為 0 或無法取得、可用不合理為該磁碟 `Error`／`DataError`；尚未就緒或讀取失敗為該磁碟 `Error`／`TransientError`；不影響其他磁碟 |
| 整體 | 依多裝置整體狀態規則；沒有任何固定磁碟為 `Unavailable`；列舉失敗為 `Error`／`TransientError` |
| 告警 | 各磁碟可用空間告警自此生效（§6） |

### 4.4 程序（Phase 2-4）

| 項目 | 規格 |
|---|---|
| 來源 | `System.Diagnostics.Process`；每個程序物件讀完即釋放 |
| 排序 | Working Set 由大到小，相同時依 PID；取前 `process.topCount`（1～20，預設 5）名，排名從 1 起算 |
| 數值 | 程序名稱、PID、Working Set（MB，1,048,576 bytes）、啟動時間（UTC，輔助辨識 PID 重用；只讀前 N 名） |
| 部分失敗 | 列舉後結束或 PID／Working Set 無法讀取：略過並計入 `SkippedProcessCount`；啟動時間無權讀取：留空並計入 `MissingFieldCount`；以上狀態仍為 `OK` |
| 失敗 | 列舉失敗為 `Error`／`TransientError`；沒有任何可讀程序為 `Error`／`AccessDenied` |
| 說明 | 不提供 CPU 排序；Working Set 加總不等於記憶體使用量 |
| 隱私 | 程序名稱可能揭露使用者使用的軟體，只寫入本機 CSV（沿用 v1.5 §7.4） |

### 4.5 磁碟 I/O（Phase 2-5）

| 項目 | 規格 |
|---|---|
| 來源 | PDH 萬用字元計數器 `\LogicalDisk(*)\…`、`\PhysicalDisk(*)\…`，以英文名稱加入（不受語系影響）；每次收集讀取當時存在的所有執行個體 |
| 對象 | 邏輯磁碟（`Scope = Logical`）：與磁碟容量模組相同的固定磁碟，以代號識別（`C:`）；固定磁碟無法列舉時改用計數器中符合 `X:` 的執行個體。實體磁碟（`Scope = Physical`）：`_Total` 以外的所有執行個體，名稱沿用 Windows（例如 `0 C: D:`、沒有代號時為 `1`）。先列邏輯磁碟（依代號），再列實體磁碟（依編號） |
| 數值 | 讀取／寫入 MB/s（`Disk Read/Write Bytes/sec` ÷ 1,048,576）；讀取／寫入 IOPS（`Disk Reads/Writes/sec`）；讀取／寫入延遲 ms（`Avg. Disk sec/Read/Write` × 1000）；忙碌率 = 100 − `% Idle Time`，限制在 0～100 |
| 暖機 | 速率型計數器第一次讀取為 `WarmingUp`；啟動時與 CPU 一起暖機 1 秒，第一筆即有數值；新出現的磁碟第一次也是 `WarmingUp` |
| 異常 | 固定磁碟沒有計數器資料（例如剛插上）為該磁碟 `Unavailable`；單一計數器讀取失敗或數值為負、NaN 為該磁碟 `Error`／`DataError`；不影響其他磁碟 |
| 失敗 | PDH 查詢失敗為 `Error`／`TransientError`，下次取樣重新建立查詢；無 pdh.dll 為 `Unavailable` |
| 整體 | 依多裝置整體狀態規則；沒有任何磁碟為 `Unavailable` |
| 說明 | 延遲是計數器層級的平均服務時間，不等同應用程式端到端延遲；邏輯與實體磁碟是同一份 I/O 的兩種角度，不可相加 |

### 4.6 NVIDIA GPU（Phase 2-6）

| 項目 | 規格 |
|---|---|
| 來源 | `nvidia-smi --query-gpu=index,name,utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits`；參數固定，不接受使用者輸入 |
| 尋找 | **只找兩個固定位置**（2026-11-13 確認，與 v1.4 不同）：系統目錄 `C:\Windows\System32\nvidia-smi.exe`（新版驅動程式）→ `%ProgramFiles%\NVIDIA Corporation\NVSMI\nvidia-smi.exe`（舊版驅動程式）。不搜尋 PATH，避免執行使用者可寫入資料夾中的同名程式。每次取樣重新尋找，驅動程式安裝或恢復後自動回到 `OK` |
| 執行 | `System.Diagnostics.Process`，參數以清單傳入、不開視窗、非同步讀取輸出；`timeouts.gpuSeconds`（預設 3 秒）逾時即結束程序（含子程序）並回報 `Error`／`Timeout`；停止監控時同樣結束程序。同一時間只有一個 nvidia-smi |
| 解析 | 第一欄為 index、最後四欄為數值、中間合併為名稱（名稱可能含逗號）；Invariant Culture；依 index 排序 |
| 數值 | 使用率（%）、顯存已使用／總量（MB，nvidia-smi 原值）、溫度（°C） |
| 不支援欄位 | `[N/A]`、`N/A`、`[Not Supported]`、`[Unknown Error]`、`[Insufficient Permissions]` 留空，GPU 仍為 `OK`（沿用 v1.4） |
| 異常 | 欄位數不足、index 不是整數、非數字、使用率超出 0～100、顯存為負或大於總量、溫度超出 0～150°C → 該 GPU `Error`／`DataError`，其他 GPU 照常 |
| 無法使用 | 找不到 nvidia-smi 或回報沒有裝置 → `Unavailable`；非零結束碼 → `Error`／`TransientError`（附輸出摘要）；無法啟動 → `Error`／`TransientError`；沒有輸出 → `Error`／`DataError` |
| 溫度紀錄 | 寫入 TemperatureMetrics（`SensorType = Gpu`），見 [Data-Schema](Data-Schema.md) §3.4 |
| 不支援 | AMD／Intel GPU（`Unavailable`） |

### 4.7 網路（Phase 2-7）

| 項目 | 規格 |
|---|---|
| 來源 | Win32 `GetIfTable2`（iphlpapi.dll，不需 WMI；盤點 M7）；累積位元組 `InOctets`／`OutOctets`，連線速度 `ReceiveLinkSpeed`／`TransmitLinkSpeed` |
| 識別 | `InterfaceGuid`；名稱為 Alias（例如 `Ethernet`、`Wi-Fi`），描述為驅動程式名稱 |
| 欄位對應（2026-11-13 確認） | 舊版 `Hidden` → 篩選層介面（`FilterInterface`）或回送介面，**一律不列入**，`includeNames = ["*"]` 也不納入（避免同一張網卡被重複計算）。實體介面卡 → `HardwareInterface` 且 `ConnectorPresent`，且名稱與描述都不符合內建虛擬關鍵字。連線中 → `OperStatus = Up` |
| 篩選 | 沿用 v1.5 §3.6.1（v1.4 決策）：`excludeNames` 優先；`includeNames` 有內容時取代預設判斷；否則只納入連線中、且（`physicalOnlyByDefault`）為實體的介面卡。萬用字元 `*`、`?`，不分大小寫，名稱或描述任一整段符合即算 |
| 虛擬關鍵字 | Hyper-V、Virtual、VPN、TAP-、WireGuard、Wintun、WSL、Docker、Loopback、Teredo、ISATAP、6to4、Tunnel、VMware、VirtualBox、Npcap、WAN Miniport、Tailscale、ZeroTier、Kernel Debug |
| 速度 | `bytes × 8 ÷ 秒 ÷ 1,000,000`（十進位 Mbps），以單調時間計算經過秒數 |
| 暖機與重設 | 第一次取樣、名稱或描述改變、計數器變小、經過時間不大於 0、超過連線速度 1.1 倍 → `WarmingUp`（不顯示 0 或負值）；取不到連線速度時不做尖峰檢查。啟動時與 CPU 一起暖機 1 秒 |
| 指定介面卡 | `includeNames` 指定但未連線 → 該介面卡 `Unavailable`（not connected）並捨棄基準；樣式完全找不到介面卡 → 以樣式名稱顯示 `Unavailable`（not found） |
| 合計 | 只加總 `OK` 的介面卡；畫面標示 n / m 張；沒有 `OK` 時為空。`aggregateIncludedAdaptersOnly` 保留但不影響行為（只列納入的介面卡，沿用 v1.5 附錄記錄） |
| 失敗 | `GetIfTable2` 失敗 → `Error`／`TransientError`，保留基準，恢復後以實際經過時間繼續計算；沒有任何符合的介面卡 → `Unavailable` |
| 啟動 Log | 記錄篩選結果：納入的介面卡與排除的介面卡及原因（篩選層與回送介面不列出） |
| 說明 | 顯示的是介面卡流量，不代表單一應用程式流量；同時納入實體與虛擬介面卡時合計可能重複計算 |

### 4.8 ACPI 熱區（Phase 2-8）

| 項目 | 規格 |
|---|---|
| 來源 | PDH `\Thermal Zone Information(*)`（英文名稱加入，一般使用者可讀）；不使用需要系統管理員權限的 WMI `MSAcpi_ThermalZoneTemperature` |
| 計數器（2026-11-13 確認） | 優先 `High Precision Temperature`（0.1 K，Windows 10 起）；不存在或該熱區讀不到時改用 `Temperature`（K） |
| 數值 | °C = K − 273.15，四捨五入到 0.01；合理範圍 −40～150°C，超出為該熱區 `Error`／`DataError` |
| 識別 | PDH 執行個體名稱（例如 `\_TZ.THM0`），依名稱排序 |
| 無法使用 | 系統沒有 Thermal Zone Information 物件、沒有任何熱區，或有物件但沒有熱區而 PDH 回傳無資料（Windows CI 的 VM 即是如此；溫度是瞬時值，不視為暖機）→ `Unavailable`（每次取樣重試）；PDH 失敗 → `Error`／`TransientError` 並重建查詢；無 pdh.dll → `Unavailable` |
| 固定值（2026-11-13 確認） | 不偵測；許多桌上型電腦回報固定值（例如一直是 27.8°C）。規格、相容性文件與 WPF 畫面標示「ACPI 熱區不是 CPU 溫度，部分電腦回報固定值」 |
| 紀錄 | TemperatureMetrics（`SensorType = AcpiThermalZone`）；畫面與報告一律稱「ACPI 熱區」，不得稱為 CPU 溫度 |

## 5. 資料與 Log

見 [Data-Schema](Data-Schema.md) 與 [決策 0005](decisions/0005-data-storage.md)。重點：

- 位置：`%LOCALAPPDATA%\WindowsPerformanceMonitor\Data`、`\Logs`，可設定。
- 檔名：`<類型>_v<版本>_<本機日期 YYYYMMDD>.csv`。
- 格式：UTF-8 BOM、CRLF、Invariant Culture、空值不寫 0。
- 鎖定處理：每次寫入開檔即關；鎖定時 200 ms 後重試 `logging.lockedFileRetryCount` 次，仍失敗則略過該檔本次的資料、累計遺失列數，並寫入 Error Log（去重）；下個紀錄週期照常再試，成功後寫一筆恢復紀錄。未成功的資料不在記憶體中堆積。
- 寫入統計：已寫入與遺失列數、最近錯誤，供畫面醒目顯示；停止時寫入 Application Log。
- Log 格式：一行一筆 `yyyy-MM-ddTHH:mm:ss.fffzzz [INFO|WARN|ERROR] [來源] 訊息`；UTF-8 BOM；每日分檔（本機日期）；訊息內換行轉為空白。Log 本身寫入失敗只計數，不中斷監控。
- Application Log：啟動（版本、設定來源與警告、使用預設值的欄位、週期、模組啟用狀態、告警門檻、CSV／Log 目錄）、監控開始、LoopOverrun（首次寫入，持續時每 10 分鐘彙總）、停止（正常停止含本次寫入／遺失列數；未預期例外寫「監控異常結束」）。各資料來源（含網路篩選結果）已於 Phase 2 補寫，告警事件於 Phase 1c 補寫。
- Error Log 去重：各模組、各裝置、各 CSV 檔分別追蹤。`Error` 在首次發生、錯誤代碼或訊息改變時寫入（含錯誤代碼、摘要與處理方式）；相同錯誤持續時每 10 分鐘寫一筆「過去 10 分鐘又發生 N 次」。`Unavailable` 只在首次發現與內容改變時寫入。回到其他狀態時寫一筆恢復紀錄。多裝置模組沒有模組層級錯誤時，只依各裝置記錄。
- 本機時間欄位 `TimestampLocal` 只在 SystemMetrics；其他檔以 `SampleId` 對應。
- 目錄寫不進去：啟動時實際寫入、刪除一個暫存檔確認；失敗時不開始監控並提示原因，不自動改寫到其他位置。

## 6. 告警

沿用 v1.5 §6：門檻、持續時間、冷卻、遲滯、狀態機（NORMAL／PENDING／WARNING／COOLDOWN／RECOVERING／RECOVERED）、磁碟 any／all 條件、資料不可用時的行為；告警事件寫入 Application Log。呈現方式改為 WPF（Phase 3）。

**實作規則（Phase 1c）：**

| 項目 | 規則 |
|---|---|
| 對象 | `CPU`、`RAM`、每個固定磁碟（`Disk C:`…）各自獨立；快照中不再出現的對象（例如磁碟移除）移除其狀態 |
| 判斷 | 只有狀態為 OK 且有數值時才判斷：達觸發值為 Breach、達恢復值為 Recovered、其間為 Hysteresis；其他一律 Unknown，不當成高於或低於門檻 |
| 磁碟條件 | 只設定百分比時只看百分比。`any`：任一條件達低空間即觸發，所有條件達恢復值才恢復；`all`：所有條件達低空間才觸發，任一條件達恢復值即恢復 |
| 資料不可用 | PENDING 回到 NORMAL；COOLDOWN 維持（不結束、不重新通知）；RECOVERING 回到 COOLDOWN（恢復計時歸零，冷卻計時延續） |
| 時間 | 單調時間；0 表示立即（達門檻即告警、不冷卻、達恢復值即恢復） |
| 事件 | 告警（WARN）、冷卻結束後再次通知（WARN，`repeatNotificationAfterCooldown = false` 時不發）、已恢復（INFO），寫入 Application Log，來源 `Alert`，內容含目前數值與門檻 |
| 畫面資料 | 進行中的告警（COOLDOWN／RECOVERING，含開始時間＝進入 PENDING 的取樣時間）與最近 5 筆事件（新到舊，含恢復）；`alert.enabled = false` 時不評估並標示停用 |
| 行為限制 | 告警只描述監控結果，不終止程序、不清理檔案、不變更系統設定 |

## 7. 設定

檔案：`%LOCALAPPDATA%\WindowsPerformanceMonitor\settings.json`，WPF 設定頁讀寫同一檔。完整範例（數值即內建預設值）：[settings.example.json](settings.example.json)。

### 7.1 讀取規則

| 情況 | 處理 |
|---|---|
| 檔案不存在 | 使用內建預設值並警告；不自動建立檔案 |
| JSON 無法解析 | 錯誤（含行號），不開始監控 |
| 欄位型別錯誤、超出範圍、相互關係不合理 | 錯誤，不開始監控；**一次列出所有錯誤** |
| 欄位或區塊缺少 | 使用預設值，記錄在啟動 Log |
| 未知欄位 | 忽略並警告（方便發現拼字錯誤） |
| 欄位名稱 | camelCase，不分大小寫；允許註解與結尾逗號 |
| 相對路徑 | 以 `%LOCALAPPDATA%\WindowsPerformanceMonitor` 為基準 |

**寫入（Phase 3c）：** WPF 設定頁儲存時完整改寫本檔，舊檔另存 `settings.json.bak`；見 §8.1。手動編輯的註解與欄位順序不會保留。

### 7.2 欄位與驗證

| 欄位 | 預設 | 規則 |
|---|---|---|
| `monitor.refreshSeconds`、`sampleSeconds`、`logSeconds` | 2、2、10 | 大於 0 的整數 |
| `cpu`／`memory`／`disk`／`diskIo`／`gpu`／`temperature`．`enabled` | true | |
| `network.enabled`、`physicalOnlyByDefault`、`aggregateIncludedAdaptersOnly` | true | |
| `network.includeNames`、`excludeNames` | [] | 字串陣列，不可有空白字串；萬用字元 `*`、`?` |
| `process.enabled`、`loggingEnabled` | true | |
| `process.topCount` | 5 | 1～20 |
| `process.logIntervalSeconds` | 10 | 大於 0 的整數 |
| `thresholds.cpuPercent`／`cpuRecoveryPercent` | 90／85 | 0～100；恢復值必須小於觸發值 |
| `thresholds.ramPercent`／`ramRecoveryPercent` | 90／85 | 同上 |
| `thresholds.diskFreePercent`／`diskRecoveryFreePercent` | 10／12 | 0～100；恢復值必須大於觸發值 |
| `thresholds.diskFreeGB`／`diskRecoveryFreeGB` | null／null | 成對設定；觸發值 ≥ 0；恢復值必須大於觸發值 |
| `thresholds.diskConditionMode` | `any` | `any` 或 `all` |
| `alert.enabled`、`repeatNotificationAfterCooldown` | true | |
| `alert.durationSeconds`／`cooldownSeconds`／`recoveryDurationSeconds` | 10／60／10 | 0～86400 的整數 |
| `timeouts.gpuSeconds` | 3 | 1～30 |
| `logging.enabled` | true | false 時只停止 CSV，Log 照常 |
| `logging.dataDirectory`／`logDirectory` | `Data`／`Logs` | 不可空白、不可含 `< > " \| ? *` 或控制字元；`:` 只能用在磁碟代號 |
| `logging.lockedFileRetryCount` | 1 | 0～5 |

### 7.3 與舊版的差異

- 移除（新版固定行為）：`disk.driveType`、`gpu.provider`、`logging.fileRotation`、`logging.encoding`。
- 不提供（Phase 2 確認不使用 WMI，計數器路徑固定）：`cpu.counterPath`、`cpu.useCimFallback`、`timeouts.cimSeconds`。舊設定檔裡的這些欄位會被當成未知欄位，只警告不報錯。
- 新增：`diskIo.enabled`、`temperature.enabled`。
- 時間設定一律為整數秒。

## 8. 介面

| 介面 | 說明 | Phase |
|---|---|---|
| WPF | 總覽、狀態／錯誤（3a）；即時趨勢、磁碟、網路、溫度、程序（3b）；設定（3c）。見 §8.1 | 3 |
| Cli | `--self-check`、`--version`、`--help`；`--once` 取樣一次並寫 CSV／Log（行為與連續模式相同）；`--once --json` 以 JSON 輸出完整結果（取代舊版 `-PassThru`） | 0；2-1 |

### 8.1 WPF（Phase 3，決策 [0007](decisions/0007-wpf-ui.md)）

| 項目 | 規格 |
|---|---|
| 開啟 | 讀取設定、檢查資料目錄；沒有錯誤時寫啟動 Log 並**自動開始監控**。有錯誤時仍開啟視窗，上方醒目提示原因，不開始監控、「開始」不可按 |
| 開始／停止 | 上方按鈕（鍵盤 Alt+S／Alt+T）。停止時等待進行中的取樣結束、補寫最後一筆、寫停止 Log，之後不再產生新取樣；顯示「停止中…」 |
| 關閉 | 關閉主視窗即結束程式：先停止監控再結束。不做系統匣常駐 |
| 畫面更新 | 依 `monitor.refreshSeconds` 讀取最新快照（與取樣週期分開）；監控在背景執行，不阻塞 UI |
| 狀態顯示 | 中文附英文代碼，例如「正常（OK）」「無法使用（Unavailable）」；顏色：正常綠、暖機／停止中黃、無法使用／停用灰、錯誤紅。無法取得的數值顯示「—」，不顯示假數值 |
| 醒目提示 | 設定或資料目錄錯誤、監控異常結束（可再按「開始」）、CSV 有遺失列或最近錯誤 |
| 總覽（3a） | 最後取樣時間與耗時（超過週期時標示）；各模組卡片（名稱、狀態、目前數值摘要、錯誤原因）；進行中的告警 |
| 狀態與錯誤（3a） | 啟動時的設定與目錄錯誤／警告、CSV 寫入統計、各模組狀態與錯誤代碼、不是 OK 的裝置、最近 5 筆告警事件 |
| 頁面順序 | 總覽、即時趨勢、磁碟、網路、溫度、程序、狀態與錯誤、設定 |
| 即時趨勢（3b） | 最近 **5 分鐘**，只放記憶體；每次取樣記錄一筆（不是每次畫面更新）。五張 WPF 原生折線圖：CPU 與記憶體使用率（0～100%）、網路下載／上傳合計（Mbps，縱軸依最大值 ×1.1 取 1／2／5×10ⁿ）、各邏輯磁碟忙碌率（0～100%）、各 GPU 使用率（0～100%）、各 GPU 溫度（°C，自動，至少 100）。圖例顯示最新數值。**斷開**：該次無法取得（模組或裝置不是 OK）或相鄰取樣間隔超過取樣週期 2.5 倍（例如停止監控期間）時不連線；停止再開始保留舊資料。沒有任何資料時顯示原因（例如「無法使用（沒有 NVIDIA GPU 資料）」） |
| 磁碟（3b） | 容量表（磁碟、標籤、總量、已使用、可用、使用率、狀態、原因）；I/O 表（邏輯／實體、讀寫 MB/s、IOPS、延遲、忙碌率、狀態、原因），並說明延遲意義與邏輯／實體不可相加 |
| 網路（3b） | 合計（只加總 OK 的介面卡，n / m 張）；納入統計的介面卡表（名稱、描述、實體／虛擬、下載、上傳、狀態、原因）；說明篩選設定與啟動 Log |
| 溫度（3b） | GPU 與 ACPI 熱區同一張表（類型、名稱、溫度、狀態、原因）；標示「ACPI 熱區不是 CPU 溫度；部分電腦會一直回報固定值」 |
| 程序（3b） | RAM（Working Set）前 N 名表（排名、名稱、PID、Working Set、啟動時間〔本機時間〕）；略過與缺欄位數；說明 Working Set 加總不等於記憶體使用量、不提供 CPU 排序 |
| 各分頁狀態列 | 頁首顯示相關模組的狀態與原因 |
| 設定（3c，2026-11-16 確認） | 列出 settings.json **全部欄位**（§7.2），分為監控週期、監控項目、網路介面卡篩選、程序、告警門檻、告警時間、資料紀錄與逾時；每欄附範圍與欄位路徑。數值以文字輸入，按「儲存並重新啟動」時才轉換：轉換失敗、§7.2 驗證失敗或資料目錄無法寫入時列出原因、不寫入且保留輸入。通過後**完整改寫** settings.json（格式同 settings.example.json、UTF-8 無 BOM），已有檔案先另存 **settings.json.bak**（覆蓋上一份），以暫存檔寫入後取代；接著停止監控（補寫與停止 Log）並**自動重新啟動程式**套用（趨勢圖清空）。另有「復原」（回到使用中的設定）與「填入預設值」（不儲存）。啟動時設定檔有錯誤：設定頁顯示內建預設值，提醒儲存會取代原檔。設定頁不隨取樣更新，不覆蓋輸入中的內容 |
| 外觀 | 淺色；繁體中文；可縮放視窗（最小 760×480），WPF 依系統 DPI 縮放 |

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
| 7 | 程式碼簽章、ARM64 | Phase 5 評估 | Phase 5 |
