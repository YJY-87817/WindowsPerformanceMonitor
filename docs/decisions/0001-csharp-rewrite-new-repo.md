# 0001：C# 完整重寫，程式碼放在新 repo

- 狀態：已採用
- 日期：2026-11-13
- 決定者：專案負責人

## 背景

舊版是 Windows PowerShell 5.1 命令列工具（V1.0 已完成）。Phase 4 需要 WPF 圖形介面、磁碟 I/O、溫度與歷史報告。WPF 評估文件比較過「PowerShell + XAML」「C# 包裝 PowerShell」與「C# 完整重寫」等方案。

## 決定

1. 以 C# + .NET 10 + WPF 完整重寫，新版執行時不呼叫 PowerShell。
2. 程式碼放在新 repo `WindowsPerformanceMonitor`；舊 repo `Windows_PC_Performance_Monitor_Specification` 保持原狀，作為需求、演算法與測試案例的參考。
3. 不承諾讀取舊版 CSV。

## 影響

- 舊版規格 v1.5 是新版需求的起點；新版需求寫在 `docs/Specification_v2.0.md`。
- 兩個 repo 各自有 CI，互不影響。
- 參考：舊 repo `docs/plans/Phase4-Plan-v2.1.md`、`docs/evaluations/WPF-Evaluation.md`。
