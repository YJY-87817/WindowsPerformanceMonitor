# Windows Performance Monitor

Windows 個人電腦效能監控工具 V2.0：以 C# + .NET 10 + WPF 重寫舊版 PowerShell 工具。

> 目前狀態：**Phase 5b**（發行打包：單一檔案 zip 與草稿 Release 流程；使用說明與 2.0.0 發行文件開發中）。Phase 5a 已加入 Cli 連續監控 `run` 與穩定性測試工具。Phase 4 已完成歷史報告：WPF「報告」頁或主控台 `report` 產生離線 HTML，含統計摘要、SVG 趨勢圖、尖峰時段、告警事件、高記憶體程序與資料品質。Phase 3 已完成 WPF 介面。已提供 CPU、記憶體、磁碟容量、磁碟 I/O、NVIDIA GPU、網路、程序 RAM Top N、ACPI 熱區；WPF 有總覽、即時趨勢、磁碟、網路、溫度、程序、報告、狀態與錯誤、設定頁（開啟即監控；設定頁儲存後自動重新啟動套用）。主控台 `--once` 可取樣一次，`report --from 2026-11-01 --to 2026-11-07` 產生報告。

舊版（PowerShell V1.0）與需求來源：[Windows_PC_Performance_Monitor_Specification](https://github.com/YJY-87817/Windows_PC_Performance_Monitor_Specification)（規格書 v1.5、開發規劃 v2.1）。

## 文件

| 文件 | 內容 |
|---|---|
| [docs/Specification_v2.0.md](docs/Specification_v2.0.md) | 規格書（初稿） |
| [docs/Architecture.md](docs/Architecture.md) | 分層架構、依賴方向、DI 組裝、模組介面 |
| [docs/Migration-Plan.md](docs/Migration-Plan.md) | 舊版功能盤點與處置 |
| [docs/Data-Schema.md](docs/Data-Schema.md) | CSV 與 Log 格式（schema v1 初稿） |
| [docs/Hardware-Compatibility.md](docs/Hardware-Compatibility.md) | 平台、資料來源、權限與限制 |
| [docs/Testing-Plan.md](docs/Testing-Plan.md) | CI 檢查、測試層級與紀錄 |
| [docs/decisions/](docs/decisions/README.md) | 技術決策紀錄 |
| [CHANGELOG.md](CHANGELOG.md) | 版本變更 |

## 專案結構

```text
src/
  PerformanceMonitor.Core            模型、介面、列舉（net10.0）
  PerformanceMonitor.Application     監控流程、排程、告警、使用案例（net10.0）
  PerformanceMonitor.Reporting       統計與 HTML／SVG 報告（net10.0）
  PerformanceMonitor.Infrastructure  Windows 資料來源、CSV、Log（net10.0-windows）
  PerformanceMonitor.App             WPF 桌面程式（net10.0-windows）
  PerformanceMonitor.Cli             主控台程式：自我檢查與 Smoke Test（net10.0-windows）
tests/                               各層 xUnit 測試
tools/                               CI 檢查腳本
```

## 開發

需求：.NET SDK 10.0（版本由 `global.json` 指定）。

```bash
dotnet build -c Release
```

```bash
dotnet test -c Release
```

```bash
dotnet format --verify-no-changes
```

主控台程式（Windows）：取樣一次，寫入 CSV 與 Log 後結束：

```bash
dotnet run --project src/PerformanceMonitor.Cli -- --once
```

以 JSON 輸出完整結果（供腳本使用）：

```bash
dotnet run --project src/PerformanceMonitor.Cli -- --once --json
```

連續監控（按 Ctrl+C 停止；`--minutes N` 時 N 分鐘後自動停止）：

```bash
dotnet run --project src/PerformanceMonitor.Cli -- run --minutes 60
```

產生報告：

```bash
dotnet run --project src/PerformanceMonitor.Cli -- report --from 2026-11-01 --to 2026-11-07
```

與硬體無關的自我檢查：

```bash
dotnet run --project src/PerformanceMonitor.Cli -- --self-check
```

設定檔：`%LOCALAPPDATA%\WindowsPerformanceMonitor\settings.json`（不存在時使用內建預設值；範例見 [docs/settings.example.json](docs/settings.example.json)）。環境變數 `PERFORMANCEMONITOR_ROOT` 可改用其他資料根目錄（設定檔、CSV、Log、報告都在其下）。

穩定性測試（Windows、PowerShell 7；預設 24 小時，判定規則見 [決策 0009](docs/decisions/0009-phase5-stability-and-release.md)）：

```bash
pwsh ./tools/Test-Stability.ps1 -Target Cli -Cli publish/Cli/PerformanceMonitor.Cli.exe
```

WPF 版本（含畫面更新與趨勢圖）：

```bash
pwsh ./tools/Test-Stability.ps1 -Target App -App publish/App/PerformanceMonitor.exe -Cli publish/Cli/PerformanceMonitor.Cli.exe -DurationMinutes 60
```

WPF 程式只能在 Windows 執行：

```bash
dotnet run --project src/PerformanceMonitor.App
```

發行打包（self-contained 單一檔案，產生 `artifacts/WindowsPerformanceMonitor-<版本>-win-x64.zip` 與 `SHA256SUMS.txt`）：

```bash
pwsh ./tools/package.ps1
```

發行新版：

1. 修改 `Directory.Build.props` 的 `Version`，並在 `CHANGELOG.md` 加入 `## [版本] - 日期` 段落。
2. 合併到 main 後推 tag（例如 `v2.0.0`；含「-」的 tag 例如 `v2.0.0-rc.1` 標為預覽版）。
3. Release workflow 會檢查版本一致、建置、測試、打包、Smoke Test，並建立**草稿** Release（附 zip、SHA-256、CHANGELOG 段落與 SmartScreen 說明）；確認後在 GitHub 發布。

## CI

每次推送在 `windows-latest` 執行 8 項檢查（Build Check 產生單一檔案 zip 並上傳為 artifact `package`，Smoke Test 使用同一份發行檔）：Lint、Format、Type Check、Unit Test、Build Check、Git Diff Check、Smoke Test、穩定性（Cli 連續監控 3 分鐘，報告上傳為 artifact）。另有手動觸發的「穩定性測試」workflow（Cli 或 WPF，最長 300 分鐘）。詳見 [Testing-Plan](docs/Testing-Plan.md)。

## 資料位置與隱私

監控資料預設存在 `%LOCALAPPDATA%\WindowsPerformanceMonitor\`。程序名稱可能揭露使用者使用的軟體，資料只留在本機，不上傳。
