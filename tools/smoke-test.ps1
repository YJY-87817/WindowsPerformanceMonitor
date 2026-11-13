#Requires -Version 7
<#
.SYNOPSIS
  Smoke Test：執行發行後的主控台程式。
  1. --self-check 必須通過。
  2. （僅 Windows）--once --json 實際取樣一次：
     CPU 必須為 OK 且使用率介於 0～100；SystemMetrics CSV 有 BOM、標題列且含本次 SampleId；
     Application Log 有啟動、CPU 來源與「單次取樣完成」。
.PARAMETER Exe
  PerformanceMonitor.Cli 執行檔路徑。
#>
param(
    [Parameter(Mandatory)]
    [string] $Exe
)

$ErrorActionPreference = 'Stop'
$script:Failed = $false

function Write-Pass([string] $Message) { Write-Host "  $Message" }
function Write-Fail([string] $Message) { Write-Host "  [失敗] $Message" -ForegroundColor Red; $script:Failed = $true }

# 1. --self-check
$selfCheck = & $Exe --self-check
if ($LASTEXITCODE -ne 0 -or -not ($selfCheck -match '^Self-check: OK')) {
    Write-Fail "--self-check 失敗（結束碼 $LASTEXITCODE）：$($selfCheck -join ' | ')"
} else {
    Write-Pass '--self-check：OK'
}

if (-not $IsWindows) {
    Write-Pass '非 Windows：略過 --once 實際取樣'
    exit [int]$script:Failed
}

# 2. --once --json
$raw = & $Exe --once --json
if ($LASTEXITCODE -ne 0) {
    Write-Fail "--once --json 結束碼 $LASTEXITCODE"
    exit 1
}
$json = $raw -join "`n"
$result = $json | ConvertFrom-Json
Write-Pass "SampleId：$($result.sampleId)，耗時 $($result.sampleDurationMs) ms"

$cpu = $result.modules | Where-Object module -EQ 'Cpu'
if ($null -eq $cpu) {
    Write-Fail '結果中沒有 Cpu 模組'
} elseif ($cpu.status -ne 'OK' -or $null -eq $cpu.usagePercent -or $cpu.usagePercent -lt 0 -or $cpu.usagePercent -gt 100) {
    Write-Fail "Cpu 狀態 $($cpu.status)、使用率 $($cpu.usagePercent)、錯誤 $($cpu.errorMessage)"
} else {
    Write-Pass "Cpu：OK，$($cpu.usagePercent)%"
}

if ($result.csv.lostRows -ne 0 -or $result.csv.writtenRows -lt 1) {
    Write-Fail "CSV 寫入 $($result.csv.writtenRows) 列、遺失 $($result.csv.lostRows) 列：$($result.csv.lastError)"
}

# CSV 與 Log 位於預設資料目錄（%LOCALAPPDATA%\WindowsPerformanceMonitor）。
$root = Join-Path $env:LOCALAPPDATA 'WindowsPerformanceMonitor'
# 檔名日期取自 JSON 原文的本機時間（避免 ConvertFrom-Json 自動轉換時區）。
if ($json -notmatch '"timestampLocal":\s*"(\d{4})-(\d{2})-(\d{2})') {
    Write-Fail 'JSON 沒有 timestampLocal'
    exit 1
}
$date = "$($Matches[1])$($Matches[2])$($Matches[3])"

$csvPath = Join-Path $root "Data\SystemMetrics_v1_$date.csv"
if (-not (Test-Path -LiteralPath $csvPath)) {
    Write-Fail "找不到 $csvPath"
} else {
    $bytes = [System.IO.File]::ReadAllBytes($csvPath)
    $lines = [System.IO.File]::ReadAllLines($csvPath)
    if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) {
        Write-Fail 'SystemMetrics CSV 沒有 UTF-8 BOM'
    } elseif (-not $lines[0].StartsWith('SampleId,TimestampUtc,TimestampLocal,')) {
        Write-Fail "SystemMetrics CSV 標題列不符：$($lines[0])"
    } elseif (-not ($lines | Where-Object { $_.StartsWith("$($result.sampleId),") })) {
        Write-Fail 'SystemMetrics CSV 沒有本次 SampleId'
    } else {
        Write-Pass "SystemMetrics CSV：BOM、標題列、本次資料列（共 $($lines.Count - 1) 列）"
    }
}

$logPath = Join-Path $root "Logs\Application_$date.log"
if (-not (Test-Path -LiteralPath $logPath)) {
    Write-Fail "找不到 $logPath"
} else {
    $log = Get-Content -LiteralPath $logPath -Encoding utf8
    foreach ($expected in @('[Startup] Windows Performance Monitor', '[Startup] CPU 來源：PDH', '[Monitor] 單次取樣完成')) {
        if (-not ($log | Where-Object { $_.Contains($expected) })) {
            Write-Fail "Application Log 缺少「$expected」"
        }
    }
    if (-not $script:Failed) {
        Write-Pass 'Application Log：啟動、CPU 來源、單次取樣完成'
    }
}

if ($script:Failed) { exit 1 }
Write-Pass '通過'
