#!/usr/bin/env bash
# Git Diff Check：空白錯誤、合併衝突標記，以及禁止提交監控資料與 Log。
set -uo pipefail
cd "$(dirname "$0")/.."

failed=0
empty_tree=$(git hash-object -t tree /dev/null)

if ! git -c core.quotepath=off diff --check "$empty_tree" HEAD; then
  echo "  [失敗] 已提交內容有空白錯誤或殘留的合併衝突標記（見上方輸出）。"
  failed=1
fi
if ! git -c core.quotepath=off diff --check --cached; then
  echo "  [失敗] 暫存區有空白錯誤（見上方輸出）。"
  failed=1
fi
if ! git -c core.quotepath=off diff --check; then
  echo "  [失敗] 工作目錄有空白錯誤（見上方輸出）。"
  failed=1
fi

forbidden=$(git -c core.quotepath=off ls-files | grep -E -i '(^|/)(Data|Logs)/|\.(csv|log)$' || true)
if [[ -n "$forbidden" ]]; then
  echo "  [失敗] 不得提交監控資料或 Log："
  echo "$forbidden" | sed 's/^/    /'
  failed=1
fi

if [[ "$failed" -eq 0 ]]; then
  echo "  通過：$(git ls-files | wc -l | tr -d ' ') 個追蹤檔案。"
fi
exit "$failed"
