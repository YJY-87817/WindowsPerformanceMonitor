#!/usr/bin/env bash
# Smoke Test：執行發行後的主控台程式，確認能建立服務並通過自我檢查。
# 用法：tools/smoke-test.sh <PerformanceMonitor.Cli 執行檔路徑>
set -uo pipefail

exe="${1:?請指定 PerformanceMonitor.Cli 執行檔路徑}"
output=$("$exe" --self-check 2>&1)
code=$?
echo "$output" | sed 's/^/  /'

if [[ "$code" -ne 0 ]]; then
  echo "  [失敗] 結束碼 $code（預期 0）"
  exit 1
fi
if ! grep -q '^Self-check: OK' <<<"$output"; then
  echo "  [失敗] 輸出沒有 'Self-check: OK'"
  exit 1
fi
echo "  通過"
