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
| 7 | Smoke Test | `tools/smoke-test.ps1 -Exe publish/Cli/PerformanceMonitor.Cli.exe -App publish/App/PerformanceMonitor.exe`（pwsh） | `--self-check` 失敗；（Windows）WPF 30 秒內沒有顯示主視窗、開啟後 Log 沒有「監控開始」、以 UI Automation 依序切換 8 個頁面時程式結束、設定頁按「儲存並重新啟動」後舊程式未結束／沒有新主視窗／沒有寫出 settings.json／新程式未自動監控、關閉主視窗後 30 秒內沒有結束或 Log 沒有「監控正常停止」或 SystemMetrics CSV 沒有新資料列；（Windows）`--once --json` 結束碼不是 0、CPU 或記憶體不是 OK 或使用率不在 0～100、記憶體容量不合理、磁碟容量不是 OK 或沒有固定磁碟、磁碟 I/O 不是 OK 或缺少 OK 的邏輯／實體磁碟、數值為負或忙碌率超出 0～100、GPU、網路（預設篩選）或 ACPI 熱區為 Error（OK 或 Unavailable 才通過）、熱區溫度超出 −40～150°C、`includeNames = ["*"]` 時網路不是 OK 或沒有合計速度、NetworkMetrics CSV 缺該次 SampleId、程序不是 OK 或筆數不在 1～5、DiskMetrics／DiskIoMetrics／GpuMetrics／ProcessMetrics／TemperatureMetrics CSV 缺本次 SampleId、CSV 有遺失、SystemMetrics CSV 缺 BOM／標題列／本次 SampleId、Application Log 缺啟動／CPU／GPU／網路／ACPI 熱區來源／單次取樣完成 |

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
| Stability | `tools/Test-Stability.ps1`：Cli 連續監控 3 分鐘（每次 CI）；Cli 或 WPF 最長 300 分鐘（手動 workflow）；24 小時（實機） | Windows CI／實機 |
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
| Smoke Test（預設篩選不為 Error；`includeNames = ["*"]` 時 OK 且有合計、NetworkMetrics CSV） | Windows CI | Passed（預設篩選 Unavailable；`*` 時 4 / 10 張 OK，合計 0.00 / 0.00 Mbps——CI 幾乎無流量，非 0 速度未實測） |
| Wi-Fi、USB 網卡、VPN、斷線重連、與工作管理員比對 | 實機 | Not Tested |

### Phase 2-8（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：優先高精度（0.1 K）、沒有高精度計數器或讀不到時改用 K、熱區排序與各自獨立、不合理溫度、暖機、沒有熱區、有物件但無資料時為 Unavailable（不是暖機）、整組讀取失敗、加入兩個計數器、沒有熱區物件時 Unavailable 並重試、PDH 失敗後重建、無 pdh.dll、啟動 Log 說明、文字輸出 | 同上 | Passed（338 個） |
| Cli `--once` 端對端（Temperature Unavailable、TemperatureMetrics 狀態列、啟動 Log） | 同上 | Passed |
| Smoke Test（熱區為 OK 或 Unavailable、TemperatureMetrics CSV） | Windows CI | 第一次 Failed：VM 有熱區物件但沒有熱區，PDH 回傳無資料，被誤判為 WarmingUp；修正為 Unavailable 後 Passed（PDH 0x800007D5）。同一次 CI 網路 `includeNames = ["*"]` 觀察到非 0 速度 0.12 / 0.05 Mbps |
| 有 ACPI 熱區的筆電、固定值的桌上型電腦、Windows 10 | 實機 | Not Tested |

### Phase 3a（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置（含 WPF XAML 編譯）、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：監控控制器（背景執行、停止等待結束、重複開始只執行一次、停止後再開始、未開始時停止、異常結束原因與再開始、同時停止）；ViewModel（尚未取樣、卡片依模組順序含停用與無法使用、不顯示假數值、超過週期、進行中告警、狀態頁模組／裝置／最近告警／CSV、CSV 遺失醒目提示、設定錯誤時不可開始、開始／停止命令、異常結束提示與再開始）；各模組摘要；狀態中文附英文代碼；Presentation 不參考 WPF／Infrastructure | 同上 | Passed（377 個） |
| Cli `--once` 改用共用啟動步驟後端對端 | 同上 | Passed |
| Smoke Test：WPF 開啟後自動監控、關閉主視窗後停止並結束 | Windows CI | Passed（結束碼 0、CSV 新增 2 列） |
| 畫面外觀、高 DPI、鍵盤操作、長時間執行 UI 不卡頓 | Windows 實機 | Not Tested |

### Phase 3b（2026-11-13）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置（含 WPF XAML 編譯）、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：趨勢數值擷取（不是 OK 為 null、缺模組、只取邏輯磁碟、GPU 名稱重複）、只保留 5 分鐘、訂閱取樣、正規化座標、最新點在右緣、遇 null 斷開、停止期間斷開、小幅延遲不斷開、最新無法取得顯示「—」、沒有資料顯示原因、自動縱軸 1／2／5×10ⁿ、超出上限夾住、趨勢頁五張圖；磁碟、網路、溫度、程序頁的表格列與狀態列 | 同上 | Passed（404 個） |
| Smoke Test：WPF 以 UI Automation 依序切換 7 個頁面 | Windows CI | Passed（關閉後結束碼 0、CSV 新增 3 列） |
| 趨勢圖外觀與繪製效能、表格捲動、高 DPI | Windows 實機 | Not Tested |

### Phase 3c（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置（含 WPF XAML 編譯）、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：設定頁列出 settings.json 全部欄位與分區、載入目前值、儲存有效變更並重新啟動、轉換錯誤不儲存且保留輸入、驗證規則不儲存、目錄無法寫入不儲存、寫入失敗不重新啟動、復原與填入預設值、未修改時建出的設定與目前相同、設定檔有錯誤時的提示、取樣更新不覆蓋輸入；設定寫出與讀回一致（預設與自訂、中文不跳脫、null）；寫入檔案（建立目錄、無 BOM、不留暫存檔、無效舊檔另存 .bak、備份只保留上一份） | 同上 | Passed（421 個） |
| Smoke Test：WPF 切換 8 個頁面；設定頁按「儲存並重新啟動」後寫出 settings.json、舊程式結束、新程式開啟並自動監控，結束後移除設定檔 | Windows CI | Passed（CSV 新增 5 列；重新啟動後的程式讀不到結束碼，4a 改以 Log 確認） |
| 實際修改各欄位後套用（週期、門檻、網路篩選、資料目錄）、鍵盤操作 | Windows 實機 | Not Tested |

### Phase 4a（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：P95 Nearest Rank（排名、實際值、不修改輸入、空與單筆）；期間（含頭含尾、366 天上限、起日晚於訖日）；粒度對照表與時段計算（本機午夜起算）；指標只計 OK 且有數值、0 是有效值、時段平均與最大；監控時段（開始／停止、未記錄停止以最後一筆為結束、期間前開始、單次取樣、異常結束、沒有 Log 無法計算）與缺失比例；CSV 拆欄（引號、跳脫、空欄、格式錯誤）；Log 行解析；讀取全部 CSV（網路合計、狀態列、實體／邏輯磁碟、GPU 與 ACPI 溫度分組）；壞列計數、標題列不符與空檔、缺檔、沒有資料的日期、無法讀取；HTML 離線（無 script、無外部網址、CSP）、文字編碼、ACPI 熱區說明；讀取端欄位與 `CsvTables` 一致；共用讀取、輸出無 BOM 與覆蓋 | 同上 | Passed（480 個） |
| 主控台 `report` 端對端：模擬一天兩個監控時段（1,972 筆、一列壞列）產生報告並以 Chromium 開啟，無外部請求 | Linux 開發環境 | Passed（19 項指標、預期 2,042 筆、缺失 3.4%） |
| Smoke Test：`report` 產生當天報告並檢查內容（統計摘要、CPU、資料品質、監控時段、無 script 與外部網址） | Windows CI | Passed（47 項指標、實際 7 筆／預期 5 筆：發現預期筆數少算停止時補寫的一筆，4b 修正） |
| 長期間（30／366 天）大量資料的耗時與記憶體 | Windows 實機 | Not Tested |

### Phase 4b（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：尖峰時段（相鄰段合併、兩時段被中間段連起、保留 5 個、加權平均與最大、最高裝置、沒有資料）；多裝置每段取最高；程序依名稱彙總（不分大小寫、同次相加、比例、前 10 名）；告警摘要（告警到恢復、再次通知、沒有恢復紀錄算到監控結束、最近 200 筆新到舊）；告警訊息解析（告警、再次通知、已恢復、其他來源）；預期筆數含停止時補寫；SVG（沒有資料斷開、提示文字編碼、縱軸自動 1／2／5×10ⁿ、圖例、沒有資料只顯示原因、無外部網址）；報告各段順序 | 同上 | Passed（507 個） |
| 主控台 `report` 端對端：模擬一天（含告警與程序紀錄）產生報告並以 Chromium 開啟，檢視趨勢圖與各段 | Linux 開發環境 | Passed（無外部請求；報告 356 KB） |
| Smoke Test：報告含趨勢、SVG、尖峰時段、告警事件、高記憶體程序 | Windows CI | Passed（實際 7 筆／預期 8 筆：每個監控時段 ±1 筆，4c 在報告註明） |
| 7／30／366 天報告的圖表可讀性與檔案大小 | Windows 實機 | Not Tested |

### Phase 4c（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置（含 WPF XAML 編譯）、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：報告頁預設今天、快速期間（昨天、最近 7／30 天）與期間說明；起日晚於訖日、超過 366 天、未選日期不可產生；背景產生中不可重複、完成後開啟報告與資料夾、摘要；寫入失敗顯示原因且可重試；開啟失敗保留路徑；導覽 9 頁順序 | 同上 | Passed（512 個） |
| Smoke Test：WPF 切換 9 個頁面；報告頁按「今天」「產生報告」後寫出今天的報告 | Windows CI | Passed（報告產生後開啟 Edge，不影響後續檢查） |
| 報告頁外觀、DatePicker 操作、預設瀏覽器開啟、長期間產生時畫面不卡頓 | Windows 實機 | Not Tested |

### Phase 5a（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check | Linux 開發環境，.NET SDK 10.0.404 | Passed |
| 單元測試：資料根目錄預設位置與環境變數覆寫 | 同上 | Passed（513 個） |
| Cli `run --minutes 1`：連續監控、時間到正常停止、CSV 7 筆、Log 開始／正常停止 | Linux 開發環境（Windows API 無法使用，各模組為 Unavailable） | Passed |
| `Test-Stability.ps1 -Target Cli`：1 分鐘與 6 分鐘，量測、CSV／Log 檢查、Markdown 與 HTML 報告 | 同上 | Passed（見 PR 說明的記憶體數值） |
| CI 第 8 步：Cli 連續監控 3 分鐘 | Windows CI | Passed（Working Set 52～64 MB、Private Bytes 21～35 MB、Handle 約 445、執行緒 11～14；第 2.3 分鐘 GC 回收後持平） |
| 24 小時穩定性（Cli 與 WPF） | Windows 實機 | Not Tested |

### Phase 5b（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| `tools/package.ps1`：單一檔案發行（App 62.7 MB、Cli 36.7 MB）、只有 exe、zip 88.7 MB 與 SHA-256 | Linux 開發環境（交叉建置 win-x64） | Passed |
| Release workflow 的版本檢查與 CHANGELOG 段落擷取（版本一致、不一致時失敗） | Linux 開發環境（PowerShell 7） | Passed |
| Smoke Test 與 3 分鐘穩定性測試使用單一檔案發行檔（含第一次執行解壓縮原生元件） | Windows CI | Passed（Working Set 60～94 MB、Private Bytes 42～52 MB；zip artifact 約 93 MB） |
| Release workflow 實際建立草稿 Release | GitHub（推 tag） | Not Tested（5c 完成 CHANGELOG 2.0.0 後由負責人推 tag） |
| 單一檔案在 Windows 11／10 實機的第一次啟動時間、防毒軟體與 SmartScreen 行為 | Windows 實機 | Not Tested |

### Phase 5c（2026-11-16）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置、Format、Type Check、單元測試 | Linux 開發環境，.NET SDK 10.0.404 | Passed（513 個） |
| 打包內容：使用說明、CHANGELOG、第三方授權聲明、settings.example.json；版本 2.0.0-rc.1 | Linux 開發環境 | Passed |
| CHANGELOG 2.0.0-rc.1 段落擷取（Release 說明） | Linux 開發環境（PowerShell 7） | Passed |
| Release workflow 實際建立預覽版草稿（推 `v2.0.0-rc.1`） | GitHub | 第一次 Failed：版本檢查、建置、測試、打包、Smoke Test 都通過，建立草稿時 `--prerelease` 被拆成字元；修正後重推 tag 驗證 |
| 實機驗證（[清單](Manual-Test-Checklist.md) A～F） | Windows 11 繁體中文、Windows 10 22H2 實機 | Not Tested |

### Phase 6（2026-11-23）：深色主題與圖表（6.0、6a～6d）

| 項目 | 環境 | 結果 |
|---|---|---|
| Release 建置（0 警告、0 錯誤）、Format、單元測試（Core 22、Reporting 51、Application 162、Infrastructure 202、Presentation 100） | Linux 開發環境，.NET SDK 10.0.100，`EnableWindowsTargeting` | Passed（537 個） |
| 單元測試新增：停留命中與空檔、門檻線正規化與合併、seriesFilter、各裝置頁趨勢圖與無資料原因、總覽迷你走勢對應、磁碟橫條（門檻邊界、GB 條件、自訂門檻）、程序橫條比例 | 同上 | Passed |
| 深色主題對比度：文字對背景皆 ≥ 4.5:1（WCAG 公式；調整後最低 5.1:1） | 計算 | Passed |
| WPF 畫面（XAML 只確認可編譯，沒有實際顯示）：深色主題各頁與內建控制項、標題列、折線圖停留提示／時間刻度／門檻線、各裝置頁趨勢圖、總覽迷你走勢、磁碟與程序橫條 | Windows 11／10 實機，[清單](Manual-Test-Checklist.md) C7～C21 | **Not Tested（之後測試）** |
| 程式碼審查（9 項）：2 項重要已修（提示框關閉時機、日期輸入欄深色）；其餘 7 項略過，待實機判斷 | — | 見下方「待處理」 |

待處理（實機測試時確認，必要時另開修正）：

1. 日期選擇：下拉月曆刻意維持淺底深字（內建範本顏色寫死）；確認可讀。
2. 全域 Button 樣式的 `MinWidth="72"`（既有）可能讓日期選擇的下拉鈕過寬（C9）。
3. 磁碟橫條只在 Breach 時變紅，恢復區間（Hysteresis）不紅；刻線只反映百分比門檻，不含 GB 條件（C19）。
4. 兩條告警門檻接近（例如 90 與 85）時標籤可能重疊（C16）。
5. 橫條最小寬度 6 px，極小比例會被放大（C19、C20）。
6. 模組狀態為 Error／停用時總覽仍顯示過去 5 分鐘的走勢（C18）。
7. ~~新增趨勢群組缺直接的 Extract 單元測試~~：已補（磁碟讀寫只取邏輯磁碟且非 OK 為 null、介面卡下載上傳、GPU 與 ACPI 熱區溫度、模組缺少時為空）。
