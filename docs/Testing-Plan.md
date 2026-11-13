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
| 7 | Smoke Test | `tools/smoke-test.ps1 -Exe publish/Cli/PerformanceMonitor.Cli.exe`（pwsh） | `--self-check` 失敗；（Windows）`--once --json` 結束碼不是 0、CPU 或記憶體不是 OK 或使用率不在 0～100、記憶體容量不合理、磁碟容量不是 OK 或沒有固定磁碟、磁碟 I/O 不是 OK 或缺少 OK 的邏輯／實體磁碟、數值為負或忙碌率超出 0～100、GPU 或網路（預設篩選）為 Error（OK 或 Unavailable 才通過）、`includeNames = ["*"]` 時網路不是 OK 或沒有合計速度、NetworkMetrics CSV 缺該次 SampleId、程序不是 OK 或筆數不在 1～5、DiskMetrics／DiskIoMetrics／GpuMetrics／ProcessMetrics CSV 缺本次 SampleId、CSV 有遺失、SystemMetrics CSV 缺 BOM／標題列／本次 SampleId、Application Log 缺啟動／CPU／GPU／網路來源／單次取樣完成 |

之後每加入一個資料來源，Smoke Test 就加上該模組的檢查。

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

### Phase 1a（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：模型與狀態規則、設定解析與驗證、設定範例、排程格點、取樣隔離、主迴圈（以假時間驗證格點、CSV／程序紀錄時點、略過不補跑、停止補寫） | 同上 | Passed（97 個） |
| 以上 | Windows CI | 見 PR 的 CI 結果 |

### Phase 1b（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：CSV 格式、7 種 CSV 欄位與狀態列、實際寫檔（BOM、標題列只寫一次、本機日期檔名、跨午夜）、檔案鎖定重試與遺失計數、Log 格式與失敗計數、Error Log 去重與 10 分鐘彙總、LoopOverrun 彙總、啟動／停止 Log、目錄可寫入檢查 | 同上 | Passed（151 個） |
| 以上 | Windows CI | 見 PR 的 CI 結果 |
| Excel 開啟 CSV 時的鎖定行為 | Windows 實機 | Not Tested（以 FileShare.None 模擬已測） |

### Phase 1c（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：告警狀態機（移植舊版 Pester 案例：持續時間、未滿即回 NORMAL、冷卻與再次通知、不重複通知、遲滯、恢復持續時間、恢復中斷回 COOLDOWN 且冷卻延續、資料不可用、時間為 0）、磁碟 any／all 條件、對象獨立、事件內容與 Log、最近 5 筆、磁碟移除、停用 | 同上 | Passed（183 個） |
| 以上 | Windows CI | 見 PR 的 CI 結果 |
| 實際觸發告警 | Windows 實機 | Not Tested（Phase 2 接上資料來源後） |

### Phase 2-1（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：CPU 監控（英文計數器、暖機、0～100 檢查、讀取失敗、PDH 失敗後重建、無 pdh.dll）、單次取樣、文字與 JSON 輸出、啟動 Log 資料來源 | 同上 | Passed（200 個） |
| Cli `--once`／`--once --json` 端對端（設定、啟動 Log、取樣、CSV、Log） | 同上（CPU 為 Unavailable，因為沒有 pdh.dll） | Passed |
| Smoke Test 實際取樣（CPU OK、CSV、Log） | Windows CI | Passed（CPU 20.0%） |
| 繁體中文 Windows、Windows 10 | 實機 | Not Tested |

### Phase 2-2（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：記憶體計算（1024 進位、使用率、全部可用為 0%）、總量 0 與可用大於總量、API 失敗、無 kernel32、文字輸出 | 同上 | Passed（207 個） |
| Smoke Test 實際取樣（CPU、記憶體 OK） | Windows CI | Passed（CPU 12.4%、記憶體 34.4%） |

### Phase 2-3（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：磁碟容量計算與排序、容量 0／無法取得／可用不合理只影響該磁碟、未就緒、磁碟全滿為 0、沒有固定磁碟、列舉失敗、文字與 JSON 輸出 | 同上 | Passed（218 個） |
| Smoke Test 實際取樣（CPU、記憶體、磁碟 OK；DiskMetrics CSV） | Windows CI | Passed（C: 81.3%、D: 0.5%） |

### Phase 2-4（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：Top N 與排名、同值依 PID、略過與缺欄位計數、只讀前 N 名的啟動時間、沒有可讀程序、列舉失敗、真實程序列舉、文字輸出 | 同上 | Passed（226 個） |
| Smoke Test 實際取樣（加上程序 Top 5 與 ProcessMetrics CSV） | Windows CI | Passed（CPU 40.4%、記憶體 38.3%、C: 81.4%、D: 0.5%、Top 5，略過 0） |

### Phase 2-5（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：單位換算、忙碌率限制、邏輯磁碟依固定磁碟、實體磁碟排除 _Total 與排序、固定磁碟無計數器、無法列舉磁碟時改用代號執行個體、暖機、單一磁碟讀取失敗、不合理數值、整組計數器失敗、PDH 失敗後重建、加入計數器失敗、無 pdh.dll、CSV Scope／Disk 欄位、文字與 JSON 輸出 | 同上 | Passed（250 個） |
| Cli `--once` 端對端（DiskIoMetrics CSV 狀態列、啟動 Log 來源） | 同上（DiskIo 為 Unavailable，因為沒有 pdh.dll） | Passed |
| Smoke Test 實際取樣（加上磁碟 I/O 與 DiskIoMetrics CSV） | Windows CI | Passed（邏輯 C: 忙碌 37.4%、D: 13.3%；實體 0 C:、1 D:） |
| 磁碟 I/O 數值與工作管理員比對、USB 插拔、繁體中文 Windows | 實機 | Not Tested |

### Phase 2-6（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：移植舊版 Pester GPU 案例（多張 GPU、名稱含逗號、不支援欄位留空、非數字、欄位不足、index 非整數、數值不合理、不受地區設定影響、單張異常、沒有輸出、固定參數與逾時、找不到工具不執行、逾時、非零結束碼、沒有裝置、無法啟動、恢復）、溫度、只在固定位置尋找、實際執行外部程序（輸出、結束碼、逾時結束、取消、找不到檔案）、文字輸出 | 同上 | Passed（287 個） |
| Cli `--once` 端對端（GPU Unavailable、GpuMetrics 狀態列、啟動 Log 來源） | 同上 | Passed |
| Smoke Test（GPU 為 OK 或 Unavailable、GpuMetrics CSV、Log 有 GPU 來源） | Windows CI | Passed（GPU Unavailable：找不到 nvidia-smi；外部程序逾時與取消測試 Passed） |
| 真實 NVIDIA GPU（數值、溫度、多張 GPU、驅動程式重啟、逾時） | 實機 | Skipped（CI 沒有 GPU）；Not Tested |

### Phase 2-7（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：移植舊版 Pester 網路案例（預設只納入連線中實體介面卡、虛擬關鍵字、physicalOnlyByDefault、includeNames 萬用字元與大小寫、excludeNames 優先、第一次不顯示 0、Mbps 計算、真正的 0、計數器變小、識別改變、1.1 倍尖峰、無連線速度、經過時間為 0、合計只含 OK、無符合介面卡、指定介面卡未連線／找不到、重新連線、暖機、查詢失敗保留基準、停用）、篩選層與回送介面一律排除、MIB_IF_ROW2 結構大小與欄位位移、啟動 Log 篩選結果、文字輸出 | 同上 | Passed（320 個） |
| Cli `--once` 端對端（Network Unavailable、NetworkMetrics 狀態列、啟動 Log） | 同上 | Passed |
| Smoke Test（預設篩選不為 Error；`includeNames = ["*"]` 時 OK 且有合計、NetworkMetrics CSV） | Windows CI | 見 PR 的 CI 結果 |
| Wi-Fi、USB 網卡、VPN、斷線重連、與工作管理員比對 | 實機 | Not Tested |
