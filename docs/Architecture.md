# 架構

- 版本：Phase 3c（2026-11-16）
- 相關決策：[0007 WPF 介面](decisions/0007-wpf-ui.md)、[0002 分層架構](decisions/0002-layered-architecture.md)、[0003 依賴套件](decisions/0003-dependencies.md)、[0004 主控台進入點](decisions/0004-cli-entry-point.md)

## 1. 分層與依賴方向

```text
          ┌──────────────┐   ┌──────────────┐
          │ App (WPF)    │   │ Cli          │   ← 組裝點：註冊服務、啟動
          └──┬────┬──────┘   └──────┬───────┘
             │    ▼                 │
             │  ┌──────────────┐    │
             │  │ Presentation │    │      ← ViewModel、畫面文字（不參考 WPF）
             │  └──────┬───────┘    │
        ┌────┴─────────┴────┬───────┘
        ▼                   ▼
┌───────────────┐   ┌──────────────────┐
│ Application   │◀──│ Infrastructure   │   ← 實作 Core／Application 定義的介面
└───┬───────┬───┘   └────────┬─────────┘
    │       ▼                │
    │  ┌───────────┐         │
    │  │ Reporting │         │
    │  └─────┬─────┘         │
    ▼        ▼               ▼
┌──────────────────────────────────┐
│ Core（模型、介面、列舉）          │
└──────────────────────────────────┘
```

箭頭表示「參考」。規則：

1. Core 只參考 .NET 基礎類別庫。
2. Application、Reporting 與 Presentation 不參考 Infrastructure、App 或 WPF。
3. Infrastructure 實作介面，不包含流程邏輯。
4. App 與 Cli 只做組裝與呈現，不直接呼叫 Windows API、不直接寫 CSV；App 的 XAML 不放監控與資料存取邏輯（在 Presentation 的 ViewModel）。

以上規則由各測試專案的 `ArchitectureTests` 檢查。

## 2. 各專案責任

| 專案 | 目標框架 | 責任 | 不負責 |
|---|---|---|---|
| Core | `net10.0` | 資料模型、介面、列舉（狀態、錯誤分類）、共用規則 | WPF、Windows API |
| Application | `net10.0` | 監控流程、排程、告警狀態機、服務協調、使用案例（含「產生報告」） | 硬體 API 細節、統計演算法 |
| Reporting | `net10.0` | 統計（平均、最大、P95）、彙總、HTML／SVG 產生 | 採樣、檔案讀寫流程 |
| Infrastructure | `net10.0-windows` | Windows 資料來源（PDH、nvidia-smi 等）、CSV、Log、時間來源 | UI |
| Presentation | `net10.0` | ViewModel（CommunityToolkit.Mvvm）、畫面文字與數值格式 | WPF 控制項、Windows API |
| App | `net10.0-windows` | WPF Views（XAML）、轉換器、主題資源、DI 組裝、畫面更新計時器、關閉時停止監控 | 監控與資料存取邏輯 |
| Cli | `net10.0-windows` | 主控台命令（`--self-check`，Phase 2 起 `--once`）、DI 組裝 | 畫面呈現 |

## 3. DI 組裝

- 使用 `Microsoft.Extensions.Hosting`：`Host.CreateApplicationBuilder()` 建立主機。
- 每層提供一個註冊方法，組裝點依序呼叫：

```csharp
builder.Services
    .AddPerformanceMonitorApplication()      // Application 層服務
    .AddPerformanceMonitorInfrastructure();  // Windows 實作、CSV、Log、時間來源
```

- WPF：`App.OnStartup` 建立主機並從 DI 取得 `MainWindow`；`OnExit` 釋放主機。
- 背景監控在 Phase 1 以 `IHostedService`（或等效的可取消背景工作）實作，停止時取消並完成寫入。

## 4. 模組介面（初稿）

| 介面／型別 | 專案 | 說明 |
|---|---|---|
| `IClock` | Core | `UtcNow`（時間戳）、`Elapsed`（單調時間）、`DelayAsync`（等待；測試用假時間立即推進） |
| `IHostInfo` | Core | 電腦名稱、本機時區時差 |
| `IMonitor` | Core | 監控模組：`Name`、`IsEnabled`、`SourceDescription`（啟動 Log）、`WarmUpAsync`、`SampleAsync` 回傳 `ModuleResult` |
| `ModuleResult` | Core | 模組結果基底 record：`Status`、`ErrorCode`、`ErrorMessage`（第一行、最長 200 字）、`Duration`；各模組以衍生 record 加數值欄位 |
| `MonitorSnapshot` | Core | 一次完整取樣：`SampleId`、`TimestampUtc`、`TimestampLocal`、電腦名稱、各模組結果、耗時、`LoopOverrun` |
| `ISnapshotProvider` | Core | 最新快照與發布事件，給 UI 讀取 |
| `ISnapshotRecorder` | Core | 寫入歷史紀錄；Phase 1b 以 CSV 實作（目前為 `NullSnapshotRecorder`） |
| `StatusAggregator` | Core | 多裝置整體狀態規則 |
| `CpuResult`、`MemoryResult`、`DiskResult`、`DiskIoResult`、`GpuResult`、`NetworkResult`、`ProcessResult`、`TemperatureResult` | Core | 各模組結果（資料契約）；多裝置模組實作 `IDeviceResult`，裝置為 `DeviceItem` 衍生型別 |
| `IAppLog` | Core | 專用 Log 服務（Application／Error 兩個檔案） |
| `IStorageStatus` | Core | CSV 寫入／遺失列數與最近錯誤 |
| `IAlertProvider`、`AlertView`、`AlertEvent`、`ActiveAlert` | Core | 告警狀態給 UI |
| `AppSettings` | Core | 設定（各區塊為 record） |
| `SettingsParser`／`SettingsValidator` | Application | 解析 JSON、檢查型別與範圍，一次列出所有錯誤 |
| `SettingsSerializer` | Application | 寫出完整 settings.json（可由 `SettingsParser` 讀回相同設定） |
| `IntervalSchedule` | Application | 單調時間格點：超時略過錯過的格點、不補跑 |
| `SampleRunner` | Application | 依序執行模組一次，隔離失敗並組成快照 |
| `MonitoringEngine` | Application | 主迴圈：暖機、取樣、發布、依格點寫入、停止時補寫 |
| `SnapshotStore` | Application | `ISnapshotProvider` 實作 |
| `StorageStatus` | Application | `IStorageStatus` 實作 |
| `StatusLogTracker` | Application | Error Log 去重與 10 分鐘彙總 |
| `MonitoringLogObserver` | Application | 依快照把模組／裝置狀態交給去重器、記錄 LoopOverrun |
| `StartupLog` | Application | 啟動資訊寫入 Application Log |
| `AlertConditionEvaluator` | Application | 由快照判斷 CPU／RAM／各磁碟為 Breach、Recovered、Hysteresis 或 Unknown |
| `AlertStateMachine` | Application | 單一對象的告警狀態轉換 |
| `AlertService` | Application | 管理所有對象、寫告警 Log、保留最近 5 筆；`IAlertProvider` 實作 |
| `MonitoringController`、`IMonitoringSession` | Application | 在背景執行連續監控、開始／停止（停止時等待補寫與停止 Log）、異常結束原因；WPF 使用 |
| `MonitoringSession` | Application | 接上 Log 觀察者與告警、執行主迴圈（`RunAsync`）或單次取樣（`RunOnceAsync`）、寫開始／停止 Log |
| `SnapshotFormatter` | Application | 主控台文字與 JSON 輸出 |
| `SystemClock`、`HostInfo` | Infrastructure | `IClock`、`IHostInfo` 實作 |
| `AppDataPaths` | Infrastructure | 根目錄、設定檔路徑、資料／Log 目錄解析 |
| `SettingsFileLoader` | Infrastructure | 讀取 settings.json（不存在時用預設值） |
| `SettingsFileWriter` | Infrastructure | 寫入 settings.json：舊檔另存 .bak、暫存檔寫入後取代 |
| `MonitoringStartup`、`StartupCheck` | Infrastructure | 主控台與 WPF 共用：讀取設定、檢查資料目錄、寫啟動 Log |
| `CsvFormat`、`CsvTables` | Infrastructure | CSV 欄位格式與 7 種 CSV 的 schema v1 |
| `CsvFileWriter`、`CsvSnapshotRecorder` | Infrastructure | UTF-8 BOM 附加寫入、鎖定重試；`ISnapshotRecorder` 實作 |
| `DailyLogWriter` | Infrastructure | `IAppLog` 實作 |
| `StorageInitializer` | Infrastructure | 啟動前確認 CSV／Log 目錄可寫入 |
| `IPdhQueryFactory`、`IPdhQuery`、`PdhQuery` | Infrastructure | PDH 封裝（`PdhAddEnglishCounter`、LibraryImport）；單一計數器（`IPdhCounter`）與萬用字元計數器（`IPdhCounterArray`，`PdhGetFormattedCounterArray`）；介面讓監控模組可用假物件測試 |
| `CpuMonitor` | Infrastructure | CPU 使用率 |
| `IMemoryStatusSource`、`GlobalMemoryStatusSource`、`MemoryMonitor` | Infrastructure | 實體記憶體 |
| `IDriveSource`、`DriveInfoSource`、`DiskMonitor` | Infrastructure | 固定磁碟容量 |
| `DiskIoMonitor` | Infrastructure | 邏輯與實體磁碟 I/O（PDH 萬用字元計數器；邏輯磁碟清單取自 `IDriveSource`） |
| `INvidiaSmiLocator`、`NvidiaSmiLocator` | Infrastructure | 只在固定位置尋找 nvidia-smi |
| `ICommandRunner`、`ProcessCommandRunner` | Infrastructure | 執行外部命令：參數清單、非同步讀取輸出、逾時或取消時結束程序 |
| `GpuMonitor` | Infrastructure | NVIDIA GPU（使用率、顯存、溫度） |
| `INetworkInterfaceSource`、`IfTableSource`、`NetworkMonitor` | Infrastructure | 網路介面卡流量（GetIfTable2；篩選與速度計算為純函式） |
| `IProcessSource`、`SystemProcessSource`、`ProcessMonitor` | Infrastructure | 程序 RAM Top N |
| `ThermalZoneMonitor` | Infrastructure | ACPI 熱區溫度（PDH 萬用字元計數器，優先高精度） |

| `MainViewModel`、`OverviewViewModel`、`StatusViewModel`、`StartupStatus` | Presentation | 主視窗（導覽、監控狀態、開始／停止、醒目提示）、總覽、狀態與錯誤頁 |
| `UiText`、`ModuleSummary` | Presentation | 畫面文字（繁體中文）與各模組摘要 |
| `TrendHistory` | Presentation | 訂閱每次取樣，保留最近 5 分鐘趨勢數值（執行緒安全） |
| `TrendChartBuilder`、`TrendChart` | Presentation | 計算正規化座標、斷開線段、自動縱軸（純函式）；WPF 只負責繪製 |
| `TrendViewModel`、`DiskViewModel`、`NetworkViewModel`、`TemperatureViewModel`、`ProcessViewModel` | Presentation | 即時趨勢與各分頁（表格列已格式化） |
| `TrendChartControl` | App | WPF 原生折線圖（`OnRender` 依正規化座標繪製） |
| `SettingsViewModel`、`SettingField`、`ISettingsService`、`IApplicationRestart` | Presentation | 設定頁：全部欄位、轉換、驗證、儲存並重新啟動 |
| `SettingsFileService`、`ApplicationRestart` | App | 設定頁的存檔（`SettingsFileWriter`、目錄檢查）與重新啟動（停止監控 → 啟動新程式 → 結束） |

組裝點的啟動順序：`MonitoringStartup.Check`（讀取設定、檢查資料目錄）→ 將 `AppSettings` 與各 `IMonitor` 註冊到 DI →
- Cli `--once`：有錯誤時顯示並以結束碼 1 結束；否則寫啟動 Log → `MonitoringSession.RunOnceAsync`。
- App：有錯誤時仍開啟視窗並顯示原因、不開始監控；否則寫啟動 Log → `MonitoringController.Start()`（背景執行 `MonitoringSession.RunAsync`）。畫面以 `DispatcherTimer` 依 `monitor.refreshSeconds` 呼叫 `MainViewModel.Refresh()`。關閉主視窗時先 `StopAsync()` 再結束。

## 5. 執行緒與生命週期

- 採樣在背景執行，不阻塞 UI Thread；UI 以自己的更新週期讀取最新快照。
- 同一模組不得重疊執行。
- 停止監控：取消背景工作 → 等待進行中的取樣結束或逾時 → 寫入最後一筆完整快照 → 釋放資源 → 寫 Log。

## 6. 測試策略

| 層級 | 方式 |
|---|---|
| Core／Application／Reporting | 單元測試，使用手寫假物件；任何平台可執行 |
| Infrastructure | 資料來源以介面包裝，以假資料測試解析與換算；實際 Windows API 只在 Windows CI 與實機測試 |
| Presentation | ViewModel 以假服務測試（任何平台） |
| App | Windows CI 開啟／關閉 WPF 的 Smoke Test；畫面以手動測試 |
| 整合 | Cli `--self-check`（Phase 0）、`--once`（Phase 2 起）作為 Smoke Test |

詳見 [Testing-Plan.md](Testing-Plan.md)。
