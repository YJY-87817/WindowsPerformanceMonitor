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
| 磁碟容量 | `System.IO.DriveInfo`（固定磁碟） | 一般使用者 | 該磁碟 `Error`；沒有固定磁碟 `Unavailable` | Windows CI：見 Phase 2-3 PR 結果；USB 固定磁碟、BitLocker 鎖定磁碟 Not Tested |
| 磁碟 I/O | PDH `\LogicalDisk(*)\…` | 一般使用者 | 第一次 `WarmingUp`；磁碟消失 `Unavailable` | Not Tested |
| NVIDIA GPU | `nvidia-smi`（PATH，或 `%ProgramFiles%\NVIDIA Corporation\NVSMI`） | 一般使用者 | 找不到工具 `Unavailable`；逾時或格式錯誤 `Error` | Not Tested |
| AMD／Intel GPU | 不支援 | — | `Unavailable` | — |
| 網路 | Win32 `GetIfTable2`（盤點 M7 確認） | 一般使用者 | 介面卡不存在 `Unavailable` | Not Tested |
| 程序 | `Process.GetProcesses()` | 一般使用者；系統程序部分欄位無法讀取 | 略過該程序並計數 | Not Tested |
| ACPI 熱區 | PDH `\Thermal Zone Information(*)\Temperature`（Kelvin） | 一般使用者 | 沒有熱區 `Unavailable` | Not Tested |

### 2.1 已知限制

- **ACPI 熱區：** 許多桌上型電腦沒有熱區，或回傳固定值（例如一直是 27.8°C）。它不是 CPU 核心溫度。
- **WMI `MSAcpi_ThermalZoneTemperature`：** 需要系統管理員權限，不使用。
- **CPU 核心溫度：** 需要驅動程式或管理員權限，不在範圍內。
- **nvidia-smi：** 每次取樣啟動一個子程序；有逾時與結束機制。
- **虛擬介面卡：** 實體與虛擬介面卡同時納入時，合計流量可能重複計算。

## 3. 實機測試紀錄

| 日期 | 電腦 | Windows 版本／語系 | GPU | 測試項目 | 結果 |
|---|---|---|---|---|---|
| — | — | — | — | Phase 2 起填寫 | — |

需要涵蓋：繁體中文 Windows、Windows 10 22H2、有 NVIDIA GPU 的電腦、有／無 ACPI 熱區的電腦、USB 磁碟插拔。
