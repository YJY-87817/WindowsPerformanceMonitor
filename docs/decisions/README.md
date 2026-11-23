# 技術決策紀錄

每個重要技術決策一份文件，編號遞增、不刪除；決策被取代時，在舊文件標示「已被 00xx 取代」。

| 編號 | 決策 | 狀態 |
|---|---|---|
| [0001](0001-csharp-rewrite-new-repo.md) | C# 完整重寫，程式碼放在新 repo | 已採用 |
| [0002](0002-layered-architecture.md) | 分層架構與各專案目標框架 | 已採用 |
| [0003](0003-dependencies.md) | 依賴套件原則與目前清單 | 已採用 |
| [0004](0004-cli-entry-point.md) | 獨立主控台專案作為 Smoke Test 進入點 | 已採用 |
| [0005](0005-data-storage.md) | 資料目錄、檔案命名與 schema 版本 | 已採用 |
| [0006](0006-self-contained-publish.md) | self-contained 發行 | 已採用 |
| [0007](0007-wpf-ui.md) | WPF 介面的結構與範圍（Presentation 專案、開啟即監控、不做系統匣、原生圖表、深色、繁中） | 已採用 |
| [0008](0008-html-report.md) | 歷史分析與 HTML 報告（入口、統計來源、粒度、尖峰時段、資料品質、離線 HTML） | 已採用 |
| [0009](0009-phase5-stability-and-release.md) | 穩定性測試與發行（Cli `run`、隔離根目錄、判定規則、單一檔案 zip、草稿 Release、不簽章、只發 x64） | 已採用 |

格式：狀態、日期、背景、決定、影響。
