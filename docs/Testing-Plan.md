# 測試規劃與紀錄

- 版本：Phase 0（2026-11-13）

## 1. CI

`.github/workflows/ci.yml`：任何分支每次推送都在 `windows-latest` 執行下列 7 項。前一項失敗時其餘仍會執行，方便一次看到所有問題。

| # | 檢查 | 指令 | 失敗條件 |
|---|---|---|---|
| 1 | Lint | `dotnet build -c Release` | 分析器或程式碼樣式警告（警告視為錯誤） |
| 2 | Format | `dotnet format --verify-no-changes` | 格式與 `.editorconfig` 不符 |
| 3 | Type Check | `tools/check-type-settings.sh` | 有專案未開啟 `Nullable` 或未將警告視為錯誤 |
| 4 | Unit Test | `dotnet test`（含覆蓋率） | 任何測試失敗 |
| 5 | Build Check | `dotnet publish -r win-x64 --self-contained`（App、Cli） | 無法發行 |
| 6 | Git Diff Check | `tools/diff-check.sh` | 空白錯誤、合併衝突標記、提交了 CSV／Log 或 Data／Logs 資料夾 |
| 7 | Smoke Test | `tools/smoke-test.sh publish/Cli/PerformanceMonitor.Cli.exe` | `--self-check` 結束碼不是 0，或輸出沒有 `Self-check: OK` |

Phase 2 加入 `--once` 後，Smoke Test 改為實際取樣一次並檢查各模組狀態、CSV 與 Log（比照舊版）。

## 2. 本機執行

```bash
dotnet build -c Release
dotnet format --verify-no-changes
tools/check-type-settings.sh
dotnet test -c Release --no-build
tools/diff-check.sh
```

WPF 與 Windows 專屬專案可在 Linux 建置（`EnableWindowsTargeting`），但只能在 Windows 執行。

## 3. 測試層級

| 測試 | 內容 | 環境 |
|---|---|---|
| Unit | 公式、資料轉換、統計、狀態、告警、架構依賴 | 任何平台 |
| Mock | Windows API、感測器、計數器錯誤（手寫假物件） | 任何平台 |
| Integration | 模組、排程、CSV、Log、報告 | Windows CI |
| Smoke | 發行後的 Cli 實際執行 | Windows CI |
| Manual | 真實 GPU、磁碟、溫度、網路、UI | 實機 |

## 4. 結果狀態

| 狀態 | 意義 |
|---|---|
| Passed | 已成功驗證 |
| Skipped | 測試環境不具備所需硬體或條件（例如 CI 沒有 GPU） |
| Not Tested | 尚未驗證，需列入實機測試 |
| Failed | 未通過 |

CI 沒有 GPU 時，GPU 測試標示 Skipped，不得宣稱已完成實機驗證。

## 5. 紀錄

### Phase 0（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置（警告視為錯誤） | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| `dotnet format --verify-no-changes` | 同上 | Passed |
| Type Check | 同上 | Passed（10 個專案） |
| 單元測試 | 同上 | Passed（9 個） |
| self-contained 發行（App、Cli） | 同上 | Passed |
| Git Diff Check | 同上 | Passed |
| Cli `--self-check` | 同上（以 `dotnet` 執行 dll） | Passed |
| 以上 7 項 | Windows CI | 見 PR 的 CI 結果 |
| WPF 視窗開啟 | Windows 實機 | Not Tested |
