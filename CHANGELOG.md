# 變更紀錄

格式參考 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)。資料格式（CSV schema）變更會特別標示。

## [Unreleased]

### 變更

- **視窗改為深色主題**（取代淺色，不提供切換）：背景、表格、按鈕、下拉選單、捲軸、日期選擇與標題列都改成深色；圖表線色改為深色背景適用的組合。離線 HTML 報告維持淺色。

## [2.0.0-rc.1] - 2026-11-16

第一個 C# 版本（預覽版），取代 PowerShell 版 V1.0。功能已完成，發行前在實機驗證（見 docs/Manual-Test-Checklist.md）。

### 新增

- **視窗介面**（WPF，繁體中文）：總覽、即時趨勢（最近 5 分鐘）、磁碟、網路、溫度、程序、報告、狀態與錯誤、設定共 9 頁；開啟即監控，關閉視窗即停止。
- **監控項目**：CPU、記憶體、磁碟容量、**磁碟 I/O**（邏輯與實體磁碟）、NVIDIA GPU（含溫度）、網路、程序 RAM 前 N 名、**ACPI 熱區**溫度。
- **設定頁**：所有設定可在程式內修改，儲存前完整檢查，舊檔另存 `settings.json.bak`，儲存後自動重新啟動套用。
- **歷史報告**：選擇期間產生單一離線 HTML（統計摘要、SVG 趨勢圖、尖峰時段、告警事件、高記憶體程序、資料品質）；視窗的「報告」頁或主控台 `report`。
- **主控台程式**：`--once`（取樣一次，可輸出 JSON）、`run`（無介面連續監控）、`report`、`--self-check`。
- **發行**：self-contained 單一檔案，不需安裝 .NET；zip 附使用說明與 SHA-256。

### 與 V1.0 的差異

- 改用 Windows API 與英文效能計數器，不需 WMI，不受系統語系影響；一般使用者權限即可。
- CSV 改為 schema v1（檔名含版本，例如 `SystemMetrics_v1_20261116.csv`），新增磁碟 I/O 與溫度檔，分項檔加 `ErrorCode`；欄位見 docs/Data-Schema.md。
- 資料預設存在 `%LOCALAPPDATA%\WindowsPerformanceMonitor`；設定檔改為 `settings.json`（欄位精簡，見 settings.example.json）。
- 告警改在視窗顯示（規則沿用 V1.0：持續、冷卻、遲滯）。

### 已知限制

- 程式沒有數位簽章，第一次執行時 SmartScreen 會警告。
- 只支援 NVIDIA GPU；ACPI 熱區不是 CPU 核心溫度，許多桌機沒有熱區。
- 只發行 x64；Windows 11 ARM 以模擬執行，未實測。
- 24 小時穩定性、繁體中文 Windows、Windows 10 22H2、NVIDIA 顯示卡、有熱區的筆電尚待實機驗證。

<details>
<summary>開發紀錄（各 Phase）</summary>

### Phase 5c（2026-11-16）：使用說明與發行準備

- 使用說明（docs/使用說明.md）、實機驗證清單（docs/Manual-Test-Checklist.md）、第三方授權聲明（THIRD-PARTY-NOTICES.md）。
- 版本 2.0.0-rc.1；zip 改放使用說明與第三方授權聲明；README 定稿（保留一切權利）。
- `actions/upload-artifact` 升級到 v6（Node.js 24）。
- 修正 Release workflow：預覽版參數 `--prerelease` 被 PowerShell 拆成字元，導致 `v2.0.0-rc.1` 第一次建立草稿失敗（no matches found for `-`）。

### Phase 5b（2026-11-16）：發行打包與 Release 流程

- 版本號改為 2.0.0（`Directory.Build.props`）。
- `tools/package.ps1`：兩個 self-contained 單一檔案 exe（壓縮；App 約 63 MB、Cli 約 37 MB）＋ README、CHANGELOG、settings.example.json 打包為 `WindowsPerformanceMonitor-<版本>-win-x64.zip`，附 SHA256SUMS.txt。
- CI 的 Build Check 改用打包腳本，Smoke Test 與穩定性測試使用單一檔案發行檔；zip 上傳為 artifact。
- Release workflow：推 `v*` tag 時檢查版本與 CHANGELOG、建置、測試、打包、Smoke Test，建立草稿 Release（含「-」的 tag 標為預覽版）。
- `actions/upload-artifact` 升級到 v5（Node.js 20 淘汰）。

### Phase 5a（2026-11-16）：連續監控與穩定性測試

- 主控台 `run [--minutes N]`：連續監控（與 WPF 相同的 CSV、Log、告警），Ctrl+C 或時間到正常停止，每分鐘一行摘要。
- 環境變數 `PERFORMANCEMONITOR_ROOT` 可改用其他資料根目錄（Cli 與 WPF）。
- `tools/Test-Stability.ps1`（PowerShell 7）：量測 Cli 或 WPF 的 CPU、記憶體、Handle、執行緒，檢查 CSV 與 Log，產生 Markdown 與 HTML 報告；判定規則見決策 0009。
- CI 第 8 步：Cli 連續監控 3 分鐘的穩定性測試並上傳報告；新增手動觸發的長時間穩定性 workflow。

### Phase 4c（2026-11-16）：WPF 報告頁（Phase 4 完成）

- WPF 新增「報告」頁（在「程序」之後）：起訖日期與「今天／昨天／最近 7 天／最近 30 天」，背景產生報告後以預設瀏覽器開啟；顯示摘要，並有「開啟報告」「開啟資料夾」。
- 報告的預期紀錄筆數註明每個監控時段可能有 ±1 筆誤差。
- Smoke Test 以 UI Automation 在報告頁產生今天的報告；WPF 導覽為 9 頁。

### Phase 4b（2026-11-16）：報告趨勢圖、尖峰時段、告警與程序

- 6 張內嵌 SVG 趨勢圖（CPU、記憶體、邏輯磁碟忙碌率、網路合計、GPU 使用率、溫度），每段平均與最大、沒有資料斷開、滑鼠停留顯示數值，不使用 JavaScript。
- 尖峰時段：6 項指標各列前 5 個時段（相鄰段合併；磁碟與 GPU 標示最高的裝置）。
- 告警事件：讀取 Application Log，依對象摘要次數與總時間，並列出最近 200 筆。
- 高記憶體程序：ProcessMetrics 依名稱彙總前 10 名。
- 修正：預期紀錄筆數改為 ⌈時長 ÷ 間隔⌉＋1（停止時補寫的最後一筆），短時段不再低估。

### Phase 4a（2026-11-16）：報告讀取、統計與資料品質

- 主控台 `report [--from] [--to] [--out]`：讀取期間內每天的 CSV 與 Application Log，產生單一離線 HTML（預設 `Reports\Report_起日-訖日.html`）。
- 統計摘要：各指標平均、最大、P95（Nearest Rank，由 CSV 原始紀錄計算，只計入 OK 且有數值的紀錄）與有效筆數。
- 資料品質：最早／最晚採樣、實際與預期筆數（依 Log 監控時段）、缺失比例、各模組不可用比例、缺少或損毀的檔案（壞列略過並計數）、沒有資料的日期。
- HTML 不使用 JavaScript、不載入外部資源，所有文字 HTML 編碼（決策 0008）。
- Smoke Test 產生當天報告並檢查；WPF 重新啟動後的結束訊息改以「監控正常停止」Log 確認，不再顯示空白結束碼。

### Phase 3c（2026-11-16）：設定頁（Phase 3 完成）

- 設定頁列出 settings.json 全部欄位並分區；儲存前轉換、驗證並檢查資料目錄，有問題時列出原因且不寫入。
- 儲存時完整改寫 settings.json，舊檔另存 settings.json.bak；儲存後停止監控並自動重新啟動程式套用。
- 「復原」與「填入預設值」；啟動時設定檔有錯誤時，設定頁以內建預設值開始並提醒會取代原檔。
- Smoke Test 以 UI Automation 在設定頁按「儲存並重新啟動」，確認寫檔與重新啟動。

### Phase 3b（2026-11-13）：即時趨勢與各分頁

- 即時趨勢頁：最近 5 分鐘、WPF 原生折線圖（CPU 與記憶體、網路合計、各邏輯磁碟忙碌率、GPU 使用率、GPU 溫度）；無法取得或停止期間斷開，不連成假線。
- 磁碟（容量與邏輯／實體 I/O）、網路（介面卡與合計）、溫度（GPU 與 ACPI 熱區，標示不是 CPU 溫度）、程序（RAM 前 N 名）頁。
- Smoke Test 以 UI Automation 依序切換所有頁面。

### Phase 3a（2026-11-13）：WPF 監控啟停、總覽、狀態與錯誤

- 新增 `PerformanceMonitor.Presentation`（ViewModel，不參考 WPF）與測試專案；新增依賴 CommunityToolkit.Mvvm（決策 0007）。
- WPF：開啟即自動監控、開始／停止按鈕、關閉視窗先停止監控再結束；設定或目錄錯誤時顯示原因且不開始監控。
- 總覽頁（各模組卡片、進行中告警、最後取樣時間）與狀態與錯誤頁（啟動檢查、CSV 統計、模組與裝置狀態、最近告警）；淺色、繁體中文。
- `MonitoringController`：背景執行監控、停止時等待補寫與停止 Log、保留異常結束原因。
- 主控台與 WPF 共用 `MonitoringStartup`（讀取設定、檢查目錄、寫啟動 Log）。
- Smoke Test 加入 WPF 開啟／關閉檢查。

### Phase 2-8（2026-11-13）：ACPI 熱區（Phase 2 資料來源完成）

- 新增 ACPI 熱區溫度：PDH `\Thermal Zone Information(*)`，優先 `High Precision Temperature`（0.1 K），沒有時用 `Temperature`（K）；合理範圍 −40～150°C；沒有熱區（含有物件但 PDH 無資料）時 Unavailable。
- 不偵測固定值；文件與啟動 Log 說明熱區不是 CPU 溫度、部分電腦回報固定值。
- 主控台文字輸出加入熱區；Smoke Test 檢查熱區與 TemperatureMetrics CSV。

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

</details>
