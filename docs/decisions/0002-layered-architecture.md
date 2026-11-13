# 0002：分層架構與各專案目標框架

- 狀態：已採用
- 日期：2026-11-13

## 決定

| 專案 | 目標框架 | 可參考 |
|---|---|---|
| Core | `net10.0` | 無（只用基礎類別庫） |
| Reporting | `net10.0` | Core |
| Application | `net10.0` | Core、Reporting |
| Infrastructure | `net10.0-windows` | Core、Application |
| App（WPF） | `net10.0-windows` | Application、Infrastructure |
| Cli | `net10.0-windows` | Application、Infrastructure |

- Core、Application、Reporting 不綁 Windows，由編譯器確保不會用到 WPF 或 Windows 專屬 API，單元測試可在任何平台執行。
- App 與 Cli 是組裝點（Composition Root），各自呼叫 `AddPerformanceMonitorApplication()` 與 `AddPerformanceMonitorInfrastructure()` 註冊服務。
- 依賴方向由架構測試檢查（`tests/*/ArchitectureTests.cs`）。

## 共用建置設定（`Directory.Build.props`）

- `Nullable`、`ImplicitUsings` 開啟。
- `AnalysisLevel=latest-recommended`、`EnforceCodeStyleInBuild=true`、`TreatWarningsAsErrors=true`。
- `EnableWindowsTargeting=true`：Windows 專案也能在 Linux 開發環境建置（執行仍需 Windows）。
- 套件版本集中在 `Directory.Packages.props`；SDK 版本由 `global.json` 固定為 10.0 系列。

## 影響

測試專案命名允許底線（`.editorconfig` 關閉 CA1707），方便「方法_情境_結果」格式。
