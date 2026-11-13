# 架構

- 版本：Phase 0 初稿（2026-11-13）
- 相關決策：[0002 分層架構](decisions/0002-layered-architecture.md)、[0003 依賴套件](decisions/0003-dependencies.md)、[0004 主控台進入點](decisions/0004-cli-entry-point.md)

## 1. 分層與依賴方向

```text
          ┌──────────────┐   ┌──────────────┐
          │ App (WPF)    │   │ Cli          │   ← 組裝點：註冊服務、啟動
          └──────┬───────┘   └──────┬───────┘
                 │  參考             │
        ┌────────┴──────────┬───────┘
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
2. Application 與 Reporting 不參考 Infrastructure、App 或 WPF。
3. Infrastructure 實作介面，不包含流程邏輯。
4. App 與 Cli 只做組裝與呈現，不直接呼叫 Windows API、不直接寫 CSV。

以上規則由各測試專案的 `ArchitectureTests` 檢查。

## 2. 各專案責任

| 專案 | 目標框架 | 責任 | 不負責 |
|---|---|---|---|
| Core | `net10.0` | 資料模型、介面、列舉（狀態、錯誤分類）、共用規則 | WPF、Windows API |
| Application | `net10.0` | 監控流程、排程、告警狀態機、服務協調、使用案例（含「產生報告」） | 硬體 API 細節、統計演算法 |
| Reporting | `net10.0` | 統計（平均、最大、P95）、彙總、HTML／SVG 產生 | 採樣、檔案讀寫流程 |
| Infrastructure | `net10.0-windows` | Windows 資料來源（PDH、nvidia-smi 等）、CSV、Log、時間來源 | UI |
| App | `net10.0-windows` | WPF Views、ViewModels、DI 組裝 | 監控與資料存取邏輯 |
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

Phase 0 已建立：

| 介面／型別 | 專案 | 說明 |
|---|---|---|
| `IClock` | Core | `UtcNow`（時間戳）與 `Elapsed`（單調時間，排程與速率計算用） |
| `MonitorStatus` | Core | `OK`、`WarmingUp`、`Unavailable`、`Error`、`Disabled` |
| `ErrorCode` | Core | 8 種錯誤分類 |
| `SystemClock` | Infrastructure | `IClock` 實作 |
| `SelfCheckService` | Application | 主控台 `--self-check` 使用 |

Phase 1 預計加入（名稱可在實作時調整）：

| 介面 | 說明 |
|---|---|
| `IMonitor` | 監控模組共同介面：名稱、是否啟用、取樣（回傳含 `Status` 的結果） |
| `IMetricsCollector<T>` | 取得指定型別的監控資料 |
| `IMetricsRepository<T>` | 寫入與讀取資料（CSV 實作在 Infrastructure） |
| `IMonitoringScheduler` | 依單調時間格點排程，超時略過錯過的格點、不補跑 |
| `IMonitorStatusProvider` | 提供各模組目前狀態給 UI |

## 5. 執行緒與生命週期

- 採樣在背景執行，不阻塞 UI Thread；UI 以自己的更新週期讀取最新快照。
- 同一模組不得重疊執行。
- 停止監控：取消背景工作 → 等待進行中的取樣結束或逾時 → 寫入最後一筆完整快照 → 釋放資源 → 寫 Log。

## 6. 測試策略

| 層級 | 方式 |
|---|---|
| Core／Application／Reporting | 單元測試，使用手寫假物件；任何平台可執行 |
| Infrastructure | 資料來源以介面包裝，以假資料測試解析與換算；實際 Windows API 只在 Windows CI 與實機測試 |
| App | ViewModel 以假服務測試；畫面以手動測試 |
| 整合 | Cli `--self-check`（Phase 0）、`--once`（Phase 2 起）作為 Smoke Test |

詳見 [Testing-Plan.md](Testing-Plan.md)。
