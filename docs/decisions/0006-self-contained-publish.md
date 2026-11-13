# 0006：self-contained 發行

- 狀態：已採用
- 日期：2026-11-13

## 決定

以 `dotnet publish -r win-x64 --self-contained` 發行，發行檔包含 .NET Runtime，使用者不需另外安裝。

## 影響

- 發行檔較大（Phase 0 骨架：App 約 140 MB、Cli 約 80 MB，未壓縮、未裁剪）；是否改單一檔案、壓縮或裁剪於 Phase 5 評估。WPF 不支援裁剪（trimming）。
- 程式碼簽章尚未決定；未簽章的 exe 會觸發 SmartScreen 警告，於發行前確認。
- 目前只發行 win-x64；ARM64 是否支援於 Phase 5 評估。
