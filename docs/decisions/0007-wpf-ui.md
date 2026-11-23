# 0007：WPF 介面的結構與範圍

- 狀態：已採用
- 日期：2026-11-13（Phase 3 開工前，負責人確認全部照建議）

## 背景

開發規劃 v2.1 §7 列出 Phase 3 待決定事項：系統匣常駐與關閉行為、圖表做法、深色／淺色主題。另需決定 ViewModel 放置位置、介面語言與 PR 拆法。

## 決定

| 事項 | 決定 |
|---|---|
| ViewModel 位置 | 新增 `PerformanceMonitor.Presentation`（`net10.0`，參考 Core、Application、CommunityToolkit.Mvvm；**不參考 WPF 與 Infrastructure**）。App 只放 XAML 畫面、轉換器與 DI 組裝。ViewModel 單元測試可在任何平台執行 |
| 開啟／關閉 | 開啟程式即自動開始監控，有「開始／停止」按鈕；關閉主視窗時先停止監控（補寫最後一筆、寫停止 Log）再結束程式。**V2.0 不做系統匣常駐** |
| 設定或目錄錯誤 | 仍開啟視窗，以醒目提示顯示原因，不開始監控、「開始」不可按 |
| 圖表 | WPF 原生繪製簡單折線圖，不加第三方圖表套件。3b 開工前確認：時間範圍最近 5 分鐘；畫 CPU 與記憶體、網路合計、各邏輯磁碟忙碌率、GPU 使用率與溫度；停止再開始時保留舊資料、中間斷開 |
| 主題 | 視窗介面**只做深色**（2026-11-23 由原本的淺色改為深色，不提供切換）；顏色集中定義在 `App.xaml`，內建控制項（按鈕、表格、下拉、捲軸等）的深色樣式也在同一檔。文字對背景對比度皆 ≥ 4.5:1（以 WCAG 公式核對）；標題列以 DWM 深色模式屬性套用，舊版 Windows 不支援時維持原樣。不使用實驗性的 Fluent `ThemeMode`。離線 HTML 報告維持淺色（分享與列印用，見 [0008](0008-html-report.md)） |
| 介面語言 | 繁體中文；狀態代碼保留英文並附中文（例如「正常（OK）」），與 CSV、Log 一致。畫面文字集中在 `Presentation/Text/UiText.cs` |
| 畫面更新 | 組裝點以 `DispatcherTimer` 依 `monitor.refreshSeconds` 在 UI 執行緒呼叫 `MainViewModel.Refresh()`，讀取最新快照（與取樣週期分開）；監控由 `MonitoringController` 在背景執行 |
| 設定頁（3c 開工前確認） | 全部欄位分區顯示；儲存時完整改寫 settings.json 並保留 .bak；儲存後停止監控並自動重新啟動程式套用（不在程式內熱套用） |
| 報告頁（Phase 4c，見 [0008](0008-html-report.md)） | 導覽在「程序」之後，頁面順序：總覽、即時趨勢、磁碟、網路、溫度、程序、報告、狀態與錯誤、設定 |
| PR 拆法 | 3a：監控啟停、畫面更新、總覽、狀態／錯誤頁；3b：趨勢圖與磁碟、網路、溫度、程序頁；3c：設定頁 |

## 影響

- 分層：App → Presentation → Application → Core（見 [Architecture](../Architecture.md)）；`Presentation.Tests` 的架構測試檢查不參考 WPF 與 Infrastructure。
- 新增依賴 `CommunityToolkit.Mvvm`（.NET Foundation，[0003](0003-dependencies.md)）。
- 主控台與 WPF 共用 `MonitoringStartup`（讀取設定、檢查目錄、寫啟動 Log）。
- Smoke Test 在 Windows CI 開啟 WPF、確認自動開始監控，關閉主視窗後確認停止並結束。
