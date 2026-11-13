#Requires -Version 7
<#
.SYNOPSIS
  Smoke Test：執行發行後的主控台程式。
  1. --self-check 必須通過。
  2. （僅 Windows）--once --json 實際取樣一次：
     CPU、記憶體必須為 OK 且使用率介於 0～100；磁碟容量必須為 OK 且至少一個固定磁碟；
     磁碟 I/O 必須為 OK，且至少一個邏輯磁碟與一個實體磁碟為 OK、數值不為負、忙碌率介於 0～100；
     GPU 必須為 OK（數值合理）或 Unavailable（CI 沒有 NVIDIA GPU），不得為 Error；
     網路必須為 OK 或 Unavailable（CI 唯一的網卡是預設排除的 Hyper-V 介面卡），不得為 Error；
     ACPI 熱區必須為 OK（溫度 -40～150°C）或 Unavailable（許多電腦與 VM 沒有熱區），不得為 Error；
     程序 Top N 為 OK；DiskMetrics、DiskIoMetrics、GpuMetrics、ProcessMetrics、TemperatureMetrics CSV 含本次 SampleId；SystemMetrics CSV 有 BOM、標題列且含本次 SampleId；
     Application Log 有啟動、CPU、GPU、網路與 ACPI 熱區來源與「單次取樣完成」。
  3. （僅 Windows）以 settings.json 設定 network.includeNames = ["*"] 再取樣一次：
     網路必須為 OK 且有合計速度；NetworkMetrics CSV 含該次 SampleId。結束後移除設定檔。
  4. （僅 Windows，指定 -App 時）WPF 程式：開啟後自動開始監控（Application Log 有「監控開始」），
     以 UI Automation 依序切換所有頁面（程式不得因畫面錯誤而結束），
     關閉主視窗後在 30 秒內結束，且先停止監控（Log 有「監控正常停止」、SystemMetrics CSV 有補寫的資料列）。
.PARAMETER Exe
  PerformanceMonitor.Cli 執行檔路徑。
.PARAMETER App
  PerformanceMonitor（WPF）執行檔路徑；省略時不檢查 WPF。
#>
param(
    [Parameter(Mandatory)]
    [string] $Exe,

    [string] $App
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

$gpu = $result.modules | Where-Object module -EQ 'Gpu'
if ($null -eq $gpu -or $gpu.status -notin @('OK', 'Unavailable')) {
    Write-Fail "Gpu 狀態 $($gpu.status)、錯誤 [$($gpu.errorCode)] $($gpu.errorMessage)"
} elseif ($gpu.status -eq 'Unavailable') {
    Write-Pass "Gpu：Unavailable（$($gpu.errorMessage)；沒有 NVIDIA GPU 時的預期結果，GPU 實測為 Skipped）"
} else {
    foreach ($g in $gpu.gpus) {
        if ($g.status -ne 'OK' -or ($null -ne $g.usagePercent -and ($g.usagePercent -lt 0 -or $g.usagePercent -gt 100))) {
            Write-Fail "GPU $($g.gpuIndex) 狀態 $($g.status)、使用率 $($g.usagePercent)"
        }
    }
    Write-Pass "Gpu：OK，$((@($gpu.gpus) | ForEach-Object { "GPU $($_.gpuIndex) $($_.gpuName) $($_.usagePercent)% $($_.temperatureC)°C" }) -join '、')"
}

$network = $result.modules | Where-Object module -EQ 'Network'
if ($null -eq $network -or $network.status -notin @('OK', 'Unavailable')) {
    Write-Fail "Network（預設篩選）狀態 $($network.status)、錯誤 [$($network.errorCode)] $($network.errorMessage)"
} else {
    Write-Pass "Network（預設篩選）：$($network.status)，$(@($network.adapters).Count) 張介面卡$(if ($network.errorMessage) { "（$($network.errorMessage)）" })"
}

$zones = $result.modules | Where-Object module -EQ 'Temperature'
if ($null -eq $zones -or $zones.status -notin @('OK', 'Unavailable')) {
    Write-Fail "Temperature 狀態 $($zones.status)、錯誤 [$($zones.errorCode)] $($zones.errorMessage)"
} elseif ($zones.status -eq 'Unavailable') {
    Write-Pass "Temperature（ACPI 熱區）：Unavailable（$($zones.errorMessage)）"
} else {
    foreach ($z in @($zones.sensors | Where-Object status -EQ 'OK')) {
        if ($z.temperatureC -lt -40 -or $z.temperatureC -gt 150) {
            Write-Fail "熱區 $($z.sensorName) 溫度不合理：$($z.temperatureC)"
        }
    }
    Write-Pass "Temperature（ACPI 熱區）：OK，$((@($zones.sensors) | ForEach-Object { "$($_.sensorName) $($_.temperatureC)°C $($_.status)" }) -join '、')"
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

foreach ($table in @('DiskMetrics', 'DiskIoMetrics', 'GpuMetrics', 'ProcessMetrics', 'TemperatureMetrics')) {
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
    foreach ($expected in @('[Startup] Windows Performance Monitor', '[Startup] CPU 來源：PDH', '[Startup] GPU 來源：nvidia-smi', '[Startup] 網路來源：Win32 GetIfTable2；', '[Startup] ACPI 熱區來源：PDH', '[Monitor] 單次取樣完成')) {
        if (-not ($log | Where-Object { $_.Contains($expected) })) {
            Write-Fail "Application Log 缺少「$expected」"
        }
    }
    if (-not $script:Failed) {
        Write-Pass 'Application Log：啟動、CPU／GPU／網路／ACPI 熱區來源、單次取樣完成'
    }
}

# 3. 納入所有介面卡再取樣一次，驗證網路速度可計算（CI 預設篩選下沒有可納入的介面卡）。
$settingsPath = Join-Path $root 'settings.json'
$backup = if (Test-Path -LiteralPath $settingsPath) { [System.IO.File]::ReadAllBytes($settingsPath) } else { $null }
try {
    [System.IO.File]::WriteAllText($settingsPath, '{ "network": { "includeNames": ["*"] } }')
    $raw2 = & $Exe --once --json
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "includeNames=* 時 --once --json 結束碼 $LASTEXITCODE"
    } else {
        $result2 = ($raw2 -join "`n") | ConvertFrom-Json
        $network2 = $result2.modules | Where-Object module -EQ 'Network'
        $summary = "$((@($network2.adapters) | ForEach-Object { "$($_.adapterName) $($_.status)" }) -join '、')"
        if ($null -eq $network2 -or $network2.status -ne 'OK' -or $null -eq $network2.totalDownloadMbps -or $null -eq $network2.totalUploadMbps) {
            Write-Fail "Network（includeNames=*）狀態 $($network2.status)、合計 $($network2.totalDownloadMbps) / $($network2.totalUploadMbps)、錯誤 $($network2.errorMessage)、介面卡 $summary"
        } else {
            $ok = @($network2.adapters | Where-Object status -EQ 'OK').Count
            Write-Pass ("Network（includeNames=*）：OK，合計下載 {0:N2} / 上傳 {1:N2} Mbps（{2} / {3} 張）：{4}" -f $network2.totalDownloadMbps, $network2.totalUploadMbps, $ok, @($network2.adapters).Count, $summary)
        }
        $networkCsv = Join-Path $root "Data\NetworkMetrics_v1_$date.csv"
        if (-not (Test-Path -LiteralPath $networkCsv) -or -not ([System.IO.File]::ReadAllLines($networkCsv) | Where-Object { $_.StartsWith("$($result2.sampleId),") })) {
            Write-Fail "NetworkMetrics CSV 不存在或沒有 includeNames=* 那次的 SampleId：$networkCsv"
        } else {
            Write-Pass 'NetworkMetrics CSV：有本次資料列'
        }
    }
} finally {
    if ($null -ne $backup) { [System.IO.File]::WriteAllBytes($settingsPath, $backup) } else { Remove-Item -LiteralPath $settingsPath -ErrorAction SilentlyContinue }
}

# 4. WPF：開啟即監控，關閉主視窗即停止並結束。
if ($App) {
    function Get-LogCount([string] $Text) {
        if (-not (Test-Path -LiteralPath $logPath)) { return 0 }
        return @(Get-Content -LiteralPath $logPath -Encoding utf8 | Where-Object { $_.Contains($Text) }).Count
    }
    $startedBefore = Get-LogCount '[Monitor] 監控開始'
    $stoppedBefore = Get-LogCount '[Monitor] 監控正常停止'
    $rowsBefore = @([System.IO.File]::ReadAllLines($csvPath)).Count

    $process = Start-Process -FilePath (Resolve-Path -LiteralPath $App).Path -PassThru
    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    } while (-not $process.HasExited -and $process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)

    if ($process.HasExited) {
        Write-Fail "WPF 程式開啟後立即結束（結束碼 $($process.ExitCode)）"
    } elseif ($process.MainWindowHandle -eq 0) {
        Write-Fail 'WPF 程式 30 秒內沒有顯示主視窗'
        Stop-Process -Id $process.Id -Force
    } else {
        Start-Sleep -Seconds 5 # 暖機與幾次取樣
        if ((Get-LogCount '[Monitor] 監控開始') -le $startedBefore) {
            Write-Fail 'WPF 開啟後 Application Log 沒有「監控開始」（應自動開始監控）'
        } else {
            Write-Pass 'WPF：顯示主視窗並自動開始監控'
        }

        # 依序切換所有頁面，讓每個頁面的 XAML 實際載入並繪製（含趨勢圖）。
        try {
            Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
            $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
            $nav = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '頁面')))
            $items = @($nav.FindAll([System.Windows.Automation.TreeScope]::Children,
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))))
            if ($items.Count -lt 7) {
                Write-Fail "WPF 導覽只有 $($items.Count) 個頁面（應有 7 個）"
            }
            foreach ($item in $items) {
                $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                Start-Sleep -Milliseconds 1500
                $process.Refresh()
                if ($process.HasExited) {
                    Write-Fail "WPF 切換到第 $([array]::IndexOf($items, $item) + 1) 個頁面後結束（結束碼 $($process.ExitCode)）"
                    break
                }
            }
            if (-not $process.HasExited -and $items.Count -ge 7) {
                Write-Pass "WPF：依序切換 $($items.Count) 個頁面，程式正常"
            }
        } catch {
            Write-Fail "WPF 頁面切換檢查失敗：$($_.Exception.Message)"
        }

        if ($process.HasExited) {
            Write-Fail 'WPF 在關閉前已結束'
        }
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(30000)) {
            Write-Fail 'WPF 關閉主視窗後 30 秒內沒有結束'
            Stop-Process -Id $process.Id -Force
        } elseif ((Get-LogCount '[Monitor] 監控正常停止') -le $stoppedBefore) {
            Write-Fail 'WPF 關閉後 Application Log 沒有「監控正常停止」'
        } elseif (@([System.IO.File]::ReadAllLines($csvPath)).Count -le $rowsBefore) {
            Write-Fail 'WPF 監控期間與停止時沒有寫入 SystemMetrics CSV'
        } else {
            Write-Pass "WPF：關閉主視窗後停止監控並結束（結束碼 $($process.ExitCode)、CSV 新增 $(@([System.IO.File]::ReadAllLines($csvPath)).Count - $rowsBefore) 列）"
        }
    }
}

if ($script:Failed) { exit 1 }
Write-Pass '通過'
