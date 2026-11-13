#!/usr/bin/env bash
# Type Check：確認每個專案都開啟 Nullable 參考型別檢查，且警告視為錯誤。
set -euo pipefail
cd "$(dirname "$0")/.."

failed=0
while IFS= read -r project; do
  nullable=$(dotnet msbuild "$project" -getProperty:Nullable -nologo | tr -d '\r')
  warnings=$(dotnet msbuild "$project" -getProperty:TreatWarningsAsErrors -nologo | tr -d '\r')
  if [[ "$nullable" != "enable" || "$warnings" != "true" ]]; then
    echo "  [失敗] $project：Nullable=$nullable、TreatWarningsAsErrors=$warnings"
    failed=1
  else
    echo "  $project：Nullable=enable、TreatWarningsAsErrors=true"
  fi
done < <(git ls-files '*.csproj')

exit "$failed"
