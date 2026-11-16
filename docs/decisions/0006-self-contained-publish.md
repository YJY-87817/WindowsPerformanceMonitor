# 0006：self-contained 發行

- 狀態：已採用
- 日期：2026-11-13

## 決定

以 `dotnet publish -r win-x64 --self-contained` 發行，發行檔包含 .NET Runtime，使用者不需另外安裝。

## 影響

- 發行檔較大（Phase 0 骨架：App 約 140 MB、Cli 約 80 MB，未壓縮、未裁剪）。**Phase 5b 改為單一檔案並啟用壓縮**（`PublishSingleFile`、`EnableCompressionInSingleFile`、`IncludeNativeLibrariesForSelfExtract`，見 [0009](0009-phase5-stability-and-release.md)）：App 約 63 MB、Cli 約 37 MB，zip 約 89 MB；原生元件於第一次執行時解壓縮到暫存資料夾。WPF 不支援裁剪（trimming），不裁剪。由 `tools/package.ps1` 打包，CI 與 Release 使用同一份發行檔。
- 程式碼簽章：V2.0 不簽章（0009）；Release 說明 SmartScreen 的處理方式並附 SHA-256。
- 只發行 win-x64（0009）；Windows 11 ARM 可用 x64 模擬執行（未實測）。
