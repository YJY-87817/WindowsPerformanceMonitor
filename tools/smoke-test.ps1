#Requires -Version 7
<#
.SYNOPSIS
  Smoke Test：執行發行後的主控台程式。
  1. --self-check 必須通過。
  2. （僅 Windows）--once --json 實際取樣一次：
     CPU、記憶體必須為 OK 且使用率介於 0～100；磁碟容量必須為 OK 且至少一個固定磁碟；
     磁碟 I/O 必須為 OK，且至少一個邏輯磁碟與一個實體磁碟為 OK、數值不為負、忙碌率介於 0～100；
     程序 Top N 為 OK；DiskMetrics、DiskIoMetrics、ProcessMetrics CSV 含本次 SampleId；SystemMetrics CSV 有 BOM、標題列且含本次 SampleId；
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

function Test-Percent([string] $Module, [string] $Property) {
    $m = $result.modules | Where-Object module -EQ $Module
    $value = if ($null -ne $m) { $m.$Property } else { $null }
    if ($null -eq $m) {
        Write-Fail "結果中沒有 $Module 模組"
    } elseif ($m.status -ne 'OK' -or $null -eq $value -or $value -lt 0 -or $value -gt 100) {
        Write-Fail "$Module 狀態 $($m.status)、$Property $value、錯誤 $($m.errorMessage)"
    } else {
        Write-Pass "$($Module)：OK，$Property $value"
    }
    return $m
}

$null = Test-Percent 'Cpu' 'usagePercent'
$memory = Test-Percent 'Memory' 'usagePercent'
if ($null -ne $memory -and $memory.status -eq 'OK' -and -not ($memory.totalGB -gt 0 -and $memory.usedGB -ge 0 -and $memory.freeGB -ge 0)) {
    Write-Fail "Memory 容量不合理：total $($memory.totalGB)、used $($memory.usedGB)、free $($memory.freeGB)"
}

$disk = $result.modules | Where-Object module -EQ 'Disk'
if ($null -eq $disk -or $disk.status -ne 'OK' -or @($disk.disks).Count -eq 0) {
    Write-Fail "Disk 狀態 $($disk.status)、磁碟數 $(@($disk.disks).Count)、錯誤 $($disk.errorMessage)"
} else {
    foreach ($d in $disk.disks) {
        if ($d.status -ne 'OK' -or $d.totalGB -le 0 -or $d.usedPercent -lt 0 -or $d.usedPercent -gt 100) {
            Write-Fail "磁碟 $($d.drive) 狀態 $($d.status)、總量 $($d.totalGB)、使用率 $($d.usedPercent)"
        }
    }
    Write-Pass "Disk：OK，$((@($disk.disks) | ForEach-Object { "$($_.drive) $([math]::Round($_.usedPercent, 1))%" }) -join '、')"
}

$diskIo = $result.modules | Where-Object module -EQ 'DiskIo'
$ioItems = @($diskIo.disks)
$ioOk = @($ioItems | Where-Object status -EQ 'OK')
if ($null -eq $diskIo -or $diskIo.status -ne 'OK' -or
    -not ($ioOk | Where-Object scope -EQ 'Logical') -or -not ($ioOk | Where-Object scope -EQ 'Physical')) {
    Write-Fail "DiskIo 狀態 $($diskIo.status)、錯誤 $($diskIo.errorMessage)、項目 $(($ioItems | ForEach-Object { "$($_.scope) $($_.disk) $($_.status)" }) -join '、')"
} else {
    foreach ($d in $ioOk) {
        $values = @($d.readMBps, $d.writeMBps, $d.readIops, $d.writeIops, $d.readLatencyMs, $d.writeLatencyMs, $d.busyPercent)
        if (($values | Where-Object { $null -eq $_ -or $_ -lt 0 }) -or $d.busyPercent -gt 100) {
            Write-Fail "磁碟 I/O $($d.scope) $($d.disk) 數值不合理：$($values -join ', ')"
        }
    }
    Write-Pass "DiskIo：OK，$(($ioItems | ForEach-Object { "$($_.scope) $($_.disk) $($_.status) busy $([math]::Round([double]$_.busyPercent, 1))%" }) -join '、')"
}

$process = $result.modules | Where-Object module -EQ 'Process'
$top = @($process.processes)
if ($null -eq $process -or $process.status -ne 'OK' -or $top.Count -lt 1 -or $top.Count -gt 5 -or ($top | Where-Object { $_.workingSetMB -lt 0 })) {
    Write-Fail "Process 狀態 $($process.status)、筆數 $($top.Count)、錯誤 $($process.errorMessage)"
} else {
    Write-Pass "Process：OK，Top $($top.Count)（略過 $($process.skippedProcessCount)、缺欄位 $($process.missingFieldCount)）"
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

foreach ($table in @('DiskMetrics', 'DiskIoMetrics', 'ProcessMetrics')) {
    $path = Join-Path $root "Data\$($table)_v1_$date.csv"
    if (-not (Test-Path -LiteralPath $path) -or -not ([System.IO.File]::ReadAllLines($path) | Where-Object { $_.StartsWith("$($result.sampleId),") })) {
        Write-Fail "$table CSV 不存在或沒有本次 SampleId：$path"
    } else {
        Write-Pass "$table CSV：有本次資料列"
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
