# Windows Performance Monitor

Windows 個人電腦效能監控工具 V2.0：以 C# + .NET 10 + WPF 重寫舊版 PowerShell 工具。

> 目前狀態：**Phase 0**（架構、Solution 骨架與 CI）。尚未提供監控功能；主控台程式只提供 `--self-check`。

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

執行主控台自我檢查：

```bash
dotnet run --project src/PerformanceMonitor.Cli -- --self-check
```

WPF 程式只能在 Windows 執行：

```bash
dotnet run --project src/PerformanceMonitor.App
```

發行（self-contained，不需另外安裝 .NET）：

```bash
dotnet publish src/PerformanceMonitor.App -c Release -r win-x64 --self-contained -o publish/App
```

## CI

每次推送在 `windows-latest` 執行 7 項檢查：Lint、Format、Type Check、Unit Test、Build Check、Git Diff Check、Smoke Test。詳見 [Testing-Plan](docs/Testing-Plan.md)。

## 資料位置與隱私

監控資料預設存在 `%LOCALAPPDATA%\WindowsPerformanceMonitor\`。程序名稱可能揭露使用者使用的軟體，資料只留在本機，不上傳。
