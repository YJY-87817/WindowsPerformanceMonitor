# 0004：獨立主控台專案作為 Smoke Test 進入點

- 狀態：已採用
- 日期：2026-11-13

## 背景

WPF 程式是視窗程式（WinExe），在 CI 不易自動驗證，也不方便輸出到主控台。舊版 Smoke Test 執行 `PC-Monitor.ps1 -Once -PassThru`。

## 決定

新增 `PerformanceMonitor.Cli` 主控台專案，與 App 共用 Application 與 Infrastructure。

| 命令 | 提供時間 | 說明 |
|---|---|---|
| `--self-check` | Phase 0 | 建立服務並執行與硬體無關的檢查 |
| `--version`、`--help` | Phase 0 | 版本與說明 |
| `--once` | Phase 2-1 | 取樣一次、寫入 CSV／Log 後結束；設定或目錄錯誤時不取樣、結束碼 1 |
| `--once --json` | Phase 2-1 | 同上，以 JSON 輸出完整結果（取代舊版 `-PassThru`） |

結束碼：0 成功、1 檢查失敗或設定／目錄錯誤、2 參數錯誤。

## 影響

- CI 第 7 步以發行後的 `PerformanceMonitor.Cli.exe` 執行 Smoke Test。
- Phase 2-1 起，Smoke Test 以 `--once --json` 實際取樣，檢查 CPU 狀態、SystemMetrics CSV（BOM、標題列、本次 SampleId）與 Application Log，比照舊版。
