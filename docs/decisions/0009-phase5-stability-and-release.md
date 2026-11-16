# 0009：穩定性測試與發行

- 狀態：已採用
- 日期：2026-11-16（Phase 5 開工前，負責人確認全部照建議）

## 背景

開發規劃 v2.1 §9 與舊規格 v1.5 §7.2 要求：連續執行 24 小時無非預期終止、無持續資源成長；發行為 self-contained；
程式碼簽章與 ARM64 於 Phase 5 評估。舊版有 `Test-Stability.ps1`（量測 PowerShell 監控程式）。C# 版的 Cli 原本只有 `--once`，
沒有可長時間量測的連續監控入口。

## 決定

| 事項 | 決定 |
|---|---|
| PR 拆法 | 5a：Cli 連續監控 `run` 與穩定性測試工具；5b：發行打包與 Release 流程；5c：使用說明、實機驗證清單、README 定稿、CHANGELOG 2.0.0 |
| 連續監控入口 | Cli `run [--minutes N]`：與 WPF 相同的連續監控（CSV、Log、告警），Ctrl+C 或 N 分鐘後正常停止；每分鐘輸出一行摘要。也可當無介面監控使用 |
| 隔離的資料根目錄 | 環境變數 `PERFORMANCEMONITOR_ROOT` 改用其他根目錄（設定檔、CSV、Log、報告都在其下），Cli 與 WPF 都支援；穩定性測試用它避免影響使用者的設定與資料 |
| 穩定性工具 | `tools/Test-Stability.ps1`（PowerShell 7）：`-Target Cli`（`run --minutes N`）或 `-Target App`（開啟 WPF，時間到關閉主視窗）；記錄 CPU、Working Set、Private Bytes、Handle、執行緒；檢查 CSV 與 Log；產生 Markdown 報告與 HTML 報告 |
| 判定 | 失敗：非預期結束、沒有「監控正常停止」、有「監控異常結束」、SampleId 關聯不一致或重複、OK 卻空值、CSV 遺失列、CSV 缺失超過 1%（每次監控另容許 ±1 筆）。警告（不失敗）：10 分鐘後 Private Bytes／Handle 持續成長（需至少 30 分鐘的資料）、LoopOverrun、紀錄缺口、Error Log 的 ERROR。CPU／RAM 不設硬性上限，報告列平均與最大 |
| CI | 每次 CI 加跑 Cli 3 分鐘（第 8 步）並上傳報告；另有手動觸發的「穩定性測試」workflow（Cli 或 App，1～300 分鐘）。24 小時測試在實機執行 |
| 打包（5b） | 兩個 self-contained **單一檔案**（啟用壓縮）放進一個 zip，附 README 與 settings.example.json |
| Release（5b） | 推 `v*` tag 時在 Windows 建置、測試、打包（附 SHA-256），建立**草稿** GitHub Release，由負責人確認後發布；一般 CI 也上傳 zip 為 artifact |
| 程式碼簽章 | V2.0 不簽章；文件說明 SmartScreen 警告的處理方式（「其他資訊 → 仍要執行」）並提供 SHA-256 供核對 |
| ARM64 | 只發行 win-x64；Windows 11 ARM 可用 x64 模擬執行（未實測），記入 Hardware-Compatibility |

## 影響

- 規格 §8 介面加入 Cli `run`；決策 0005 的資料根目錄可由環境變數覆寫。
- 舊版功能 S6（穩定性工具）完成移植。
- CI 時間約增加 3 分鐘。
