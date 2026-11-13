# 硬體與平台相容性

- 版本：Phase 0 初稿（2026-11-13）
- 驗證狀態：Passed、Skipped、Not Tested、Failed（定義見 [Testing-Plan.md](Testing-Plan.md)）

## 1. 作業系統與執行環境

| 項目 | 規格 | 狀態 |
|---|---|---|
| Windows 11 | 主要開發與驗收平台 | Not Tested（Phase 0 只在 CI 的 Windows Server 建置與執行 `--self-check`） |
| Windows 10 22H2 | 相容性測試平台 | Not Tested |
| .NET 10 支援 Windows 10 22H2 | .NET 10 官方支援作業系統清單（dotnet/core `release-notes/10.0/supported-os.md`）列有 Windows 10 22H2；但 Windows 10 一般支援已於 2025-10-14 結束，發行前需再查核官方清單 | 待查核（Phase 5） |
| CPU 架構 | win-x64 | ARM64 於 Phase 5 評估 |
| 權限 | 一般使用者；不要求系統管理員 | — |
| .NET Runtime | self-contained，隨程式發行，不需另外安裝 | — |
| 網路 | 不需要網際網路 | — |

## 2. 資料來源

| 資料 | 來源 | 權限 | 無法取得時 | 實機狀態 |
|---|---|---|---|---|
| CPU 使用率 | PDH `\Processor(_Total)\% Processor Time`（英文名稱加入） | 一般使用者 | 第一次 `WarmingUp`；讀取失敗 `Error`；無 pdh.dll `Unavailable` | Windows CI（Windows Server，英文）Passed；Windows 11／10、繁體中文 Not Tested |
| 記憶體 | Win32 `GlobalMemoryStatusEx` | 一般使用者 | 資料不合理 `Error`（DataError）；API 失敗 `Error`（TransientError） | Windows CI Passed；Windows 11／10 Not Tested |
| 磁碟容量 | `System.IO.DriveInfo`（固定磁碟） | 一般使用者 | 該磁碟 `Error`；沒有固定磁碟 `Unavailable` | Windows CI Passed（C:、D:）；USB 固定磁碟、BitLocker 鎖定磁碟 Not Tested |
| 磁碟 I/O | PDH `\LogicalDisk(*)\…`、`\PhysicalDisk(*)\…`（萬用字元，英文名稱加入） | 一般使用者 | 第一次 `WarmingUp`；固定磁碟沒有計數器資料 `Unavailable`；讀取失敗 `Error`；無 pdh.dll `Unavailable` | Windows CI Passed（C:、D: 與實體磁碟 0、1）；繁體中文、USB 插拔、RAID／儲存空間 Not Tested |
| NVIDIA GPU | `nvidia-smi`（只找 `C:\Windows\System32`，再找 `%ProgramFiles%\NVIDIA Corporation\NVSMI`；不搜尋 PATH） | 一般使用者 | 找不到工具或沒有裝置 `Unavailable`；逾時 `Error`／`Timeout`；格式錯誤 `Error`／`DataError` | Windows CI Skipped（沒有 NVIDIA GPU；已驗證找不到工具時為 `Unavailable`）；實機 Not Tested |
| AMD／Intel GPU | 不支援 | — | `Unavailable` | — |
| 網路 | Win32 `GetIfTable2`（盤點 M7 確認） | 一般使用者 | 沒有符合的介面卡 `Unavailable`；指定的介面卡未連線或找不到 `Unavailable`；查詢失敗 `Error` | Windows CI Passed（CI 網卡皆為 Hyper-V，預設篩選下為 Unavailable；`includeNames = ["*"]` 時 10 張中 4 張 OK、有合計速度；Phase 2-8 的 CI 觀察到非 0 速度 0.12 / 0.05 Mbps，數值正確性仍待實機與工作管理員比對）；Wi-Fi、USB 網卡、VPN 實機 Not Tested |
| 程序 | `Process.GetProcesses()` | 一般使用者；系統程序的啟動時間常無法讀取 | 略過該程序或欄位並計數，狀態仍 OK | Linux 開發環境 Passed（真實程序）；Windows CI Passed（Top 5） |
| ACPI 熱區 | PDH `\Thermal Zone Information(*)\High Precision Temperature`（0.1 K），沒有時 `Temperature`（K） | 一般使用者 | 沒有熱區 `Unavailable`；溫度不合理 `Error` | Windows CI：見 Phase 2-8 PR 結果（CI 的 VM 有熱區物件但沒有熱區，預期 Unavailable）；有熱區的筆電、Windows 10 Not Tested |

### 2.1 已知限制

- **ACPI 熱區：** 許多桌上型電腦沒有熱區，或回傳固定值（例如一直是 27.8°C）。它不是 CPU 核心溫度。
- **WMI `MSAcpi_ThermalZoneTemperature`：** 需要系統管理員權限，不使用。
- **CPU 核心溫度：** 需要驅動程式或管理員權限，不在範圍內。
- **nvidia-smi：** 每次取樣啟動一個子程序（約數十到數百毫秒）；有逾時與結束機制。安裝在其他位置（例如只在 PATH 中）的 nvidia-smi 不會被使用。
- **磁碟 I/O：** 延遲是計數器層級的平均服務時間；RAID、儲存空間（Storage Spaces）的實體磁碟名稱與數量可能和實際硬碟不同。
- **虛擬介面卡：** 實體與虛擬介面卡同時納入時，合計流量可能重複計算。篩選層介面一律不列入。
- **雲端／虛擬機：** VM 的網卡（例如 Hyper-V Network Adapter）依關鍵字判斷為虛擬，預設不納入；需要時以 `includeNames` 指定。

## 3. 實機測試紀錄

| 日期 | 電腦 | Windows 版本／語系 | GPU | 測試項目 | 結果 |
|---|---|---|---|---|---|
| — | — | — | — | Phase 2 起填寫 | — |

需要涵蓋：繁體中文 Windows、Windows 10 22H2、有 NVIDIA GPU 的電腦、有／無 ACPI 熱區的電腦、USB 磁碟插拔。
