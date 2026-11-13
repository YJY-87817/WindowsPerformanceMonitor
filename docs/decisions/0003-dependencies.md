# 0003：依賴套件原則與目前清單

- 狀態：已採用
- 日期：2026-11-13

## 原則（Phase 4 規劃 v2.1 §10.1）

| 類別 | 原則 |
|---|---|
| Microsoft 官方或 .NET Foundation 套件 | 允許；每個都要列在下表並寫明用途 |
| 只用於測試的套件 | 允許；不進入發行檔，需列在下表 |
| 其他第三方套件 | 逐項評估，經負責人確認才可加入 |

## 目前清單

| 套件 | 版本 | 類別 | 使用專案 | 用途 |
|---|---|---|---|---|
| Microsoft.Extensions.Hosting | 10.0.15 | Microsoft 官方 | App、Cli | DI、設定、Logging 與背景服務生命週期 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.15 | Microsoft 官方 | Application、Infrastructure | 各層提供 `IServiceCollection` 註冊方法 |
| CommunityToolkit.Mvvm | 8.4.2 | .NET Foundation | Presentation | MVVM 基礎（`ObservableObject`、`RelayCommand`、`AsyncRelayCommand`）；不使用原始碼產生器 |
| xunit | 2.9.3 | 測試（.NET Foundation） | tests | 測試框架 |
| xunit.runner.visualstudio | 3.1.5 | 測試（.NET Foundation） | tests | `dotnet test` 執行器 |
| Microsoft.NET.Test.Sdk | 18.0.1 | 測試（Microsoft） | tests | 測試主機 |
| coverlet.collector | 6.0.4 | 測試（.NET Foundation） | tests | 覆蓋率 |

## 已確認、尚未加入

| 套件 | 預計 Phase | 用途 |
|---|---|---|
| （無） | | |

## 不使用

- Mock 套件：介面都不大，測試使用手寫假物件（Fake）。
- 圖表套件：Phase 3 確認使用 WPF 原生繪圖（[0007](0007-wpf-ui.md)），不加第三方圖表套件。
