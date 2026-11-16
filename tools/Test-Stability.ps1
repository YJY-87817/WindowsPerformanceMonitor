#Requires -Version 7.2
<#
.SYNOPSIS
  長時間穩定性測試（舊規格 v1.5 §7.2；決策 0009）。

.DESCRIPTION
  在獨立的資料根目錄（環境變數 PERFORMANCEMONITOR_ROOT，不影響使用者的設定與資料）啟動監控程式：
    -Target Cli：PerformanceMonitor.Cli run --minutes N（時間到自行正常停止）
    -Target App：PerformanceMonitor（WPF，含畫面更新與趨勢圖），時間到關閉主視窗
  每隔固定時間記錄監控程式的 CPU 時間、Working Set、Private Bytes、Handle 與執行緒數；結束後檢查
  CSV（SampleId 關聯、重複、缺口、缺失比例、OK 卻空值）與 Log（正常停止、異常結束、CSV 遺失列），
  產生 Markdown 報告；提供 -Cli 時另以 `report` 產生 HTML 報告。

  判定（2026-11-16 確認）：
    失敗（結束碼 1）：非預期結束、沒有「監控正常停止」、有「監控異常結束」、SampleId 關聯不一致或重複、
                      CSV 遺失列、CSV 缺失超過 1%（每次監控容許 ±1 筆）。
    警告（不失敗）：前 10 分鐘之後 Private Bytes 或 Handle 持續成長（需至少 30 分鐘的穩定期資料）、LoopOverrun、
                      紀錄間隔缺口、Error Log 的錯誤。
  CPU 與記憶體不設硬性上限，報告列出平均與最大值供判讀。

.PARAMETER Target
  Cli 或 App。

.PARAMETER Cli
  PerformanceMonitor.Cli.exe 路徑（Target Cli 必填；Target App 時用來產生 HTML 報告，可省略）。

.PARAMETER App
  PerformanceMonitor.exe（WPF）路徑（Target App 必填）。

.PARAMETER DurationMinutes
  測試時間（分鐘），預設 1440（24 小時）。

.PARAMETER MeasureIntervalSeconds
  量測間隔（秒），預設 60。

.PARAMETER SettingsPath
  要測試的設定檔（選用）；複製到測試根目錄，資料與 Log 目錄改為根目錄下的 Data、Logs。省略時用內建預設值。

.PARAMETER OutputDirectory
  輸出資料夾，預設 TestResults\stability-<時間>。

.EXAMPLE
  ./tools/Test-Stability.ps1 -Target Cli -Cli publish/Cli/PerformanceMonitor.Cli.exe
  24 小時 Cli 穩定性測試。

.EXAMPLE
  ./tools/Test-Stability.ps1 -Target App -App publish/App/PerformanceMonitor.exe -Cli publish/Cli/PerformanceMonitor.Cli.exe -DurationMinutes 60
  WPF 一小時穩定性測試。
#>
[CmdletBinding()]
param(
    [ValidateSet('Cli', 'App')]
    [string] $Target = 'Cli',

    [string] $Cli,

    [string] $App,

    [ValidateRange(1, 10080)]
    [int] $DurationMinutes = 1440,

    [ValidateRange(5, 3600)]
    [int] $MeasureIntervalSeconds = 60,

    [string] $SettingsPath,

    [string] $OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) ('TestResults/stability-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

if ($Target -eq 'Cli' -and -not $Cli) { throw 'Target Cli 需要 -Cli（PerformanceMonitor.Cli.exe 路徑）' }
if ($Target -eq 'App' -and -not $App) { throw 'Target App 需要 -App（PerformanceMonitor.exe 路徑）' }
if ($Cli) { $Cli = (Resolve-Path -LiteralPath $Cli).Path }
if ($App) { $App = (Resolve-Path -LiteralPath $App).Path }

#region 準備：獨立的資料根目錄

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$root = Join-Path $OutputDirectory 'root'
$null = New-Item -ItemType Directory -Path $root -Force
$dataDir = Join-Path $root 'Data'
$logDir = Join-Path $root 'Logs'

$settingsSource = '內建預設值'
$logSeconds = 10
$sampleSeconds = 2
if ($SettingsPath) {
    $settingsSource = (Resolve-Path -LiteralPath $SettingsPath).Path
    $json = Get-Content -LiteralPath $settingsSource -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    if (-not $json.ContainsKey('logging')) { $json['logging'] = @{} }
    $json['logging']['enabled'] = $true
    $json['logging']['dataDirectory'] = 'Data'
    $json['logging']['logDirectory'] = 'Logs'
    if ($json.ContainsKey('monitor')) {
        if ($json['monitor'].ContainsKey('logSeconds')) { $logSeconds = [int]$json['monitor']['logSeconds'] }
        if ($json['monitor'].ContainsKey('sampleSeconds')) { $sampleSeconds = [int]$json['monitor']['sampleSeconds'] }
    }
    $json | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'settings.json') -Encoding utf8NoBOM
}

$previousRoot = $env:PERFORMANCEMONITOR_ROOT
$env:PERFORMANCEMONITOR_ROOT = $root

#endregion

#region 測試環境

function Get-EnvironmentInfo {
    $info = [ordered]@{
        '測試日期' = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz', $invariant)
        '電腦名稱' = [System.Environment]::MachineName
        '作業系統' = [System.Environment]::OSVersion.VersionString
        'CPU' = '（無法取得）'
        'RAM' = '（無法取得）'
        'NVIDIA GPU' = '否（找不到 nvidia-smi）'
        '受測程式' = $(if ($Target -eq 'Cli') { "$Cli run --minutes $DurationMinutes" } else { $App })
        '版本' = '（無法取得）'
        '量測工具' = 'tools/Test-Stability.ps1（System.Diagnostics.Process：TotalProcessorTime、WorkingSet64、PrivateMemorySize64、HandleCount、Threads）'
    }
    if ($IsWindows) {
        try {
            $os = Get-CimInstance -ClassName Win32_OperatingSystem -OperationTimeoutSec 10
            $info['作業系統'] = '{0}（版本 {1}，組建 {2}）' -f $os.Caption, $os.Version, $os.BuildNumber
            $info['RAM'] = '{0:N1} GB' -f ([double]$os.TotalVisibleMemorySize / 1MB)
            $cpu = @(Get-CimInstance -ClassName Win32_Processor -OperationTimeoutSec 10)
            $info['CPU'] = '{0}（{1} 核心／{2} 邏輯處理器）' -f (($cpu | ForEach-Object { $_.Name.Trim() } | Select-Object -Unique) -join ' / '),
                ($cpu | Measure-Object -Property NumberOfCores -Sum).Sum, ($cpu | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
        } catch {
            $null = $_
        }
        $nvsmi = @((Join-Path $env:SystemRoot 'System32\nvidia-smi.exe'), (Join-Path $env:ProgramFiles 'NVIDIA Corporation\NVSMI\nvidia-smi.exe'))
        if ($nvsmi | Where-Object { Test-Path -LiteralPath $_ }) { $info['NVIDIA GPU'] = '是（找到 nvidia-smi）' }
    }
    if ($Cli) {
        try { $info['版本'] = (& $Cli --version) -join ' ' } catch { $null = $_ }
    }
    return $info
}

$environment = Get-EnvironmentInfo

#endregion

#region 執行與量測

$measurePath = Join-Path $OutputDirectory 'monitor-resources.csv'
Set-Content -LiteralPath $measurePath -Value 'ElapsedMinutes,CpuSeconds,WorkingSetMB,PrivateMB,Handles,Threads' -Encoding utf8
$measurements = [System.Collections.Generic.List[object]]::new()
$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$consolePath = Join-Path $OutputDirectory 'monitor-console.txt'

Write-Host "穩定性測試開始：$Target、$DurationMinutes 分鐘，每 $MeasureIntervalSeconds 秒量測一次。輸出：$OutputDirectory"
$startedAt = Get-Date
if ($Target -eq 'Cli') {
    $process = Start-Process -FilePath $Cli -ArgumentList @('run', '--minutes', $DurationMinutes) -PassThru -NoNewWindow `
        -RedirectStandardOutput $consolePath -RedirectStandardError (Join-Path $OutputDirectory 'monitor-errors.txt')
} else {
    $process = Start-Process -FilePath $App -PassThru
    $deadline = (Get-Date).AddSeconds(60)
    do {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
    } while (-not $process.HasExited -and $process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if (-not $process.HasExited -and $process.MainWindowHandle -eq 0) {
        $failures.Add('WPF 60 秒內沒有顯示主視窗')
    }
}

$clock = [System.Diagnostics.Stopwatch]::StartNew()
$endMs = $DurationMinutes * 60000.0
$nextMs = 0.0
$unexpectedExit = $null
try {
    while ($clock.Elapsed.TotalMilliseconds -lt $endMs) {
        $wait = $nextMs - $clock.Elapsed.TotalMilliseconds
        if ($wait -gt 0) {
            Start-Sleep -Milliseconds ([int][math]::Min($wait, 2000))
            continue
        }

        $process.Refresh()
        if ($process.HasExited) {
            $unexpectedExit = '監控程式在 {0:N1} 分鐘時提前結束（結束碼 {1}）' -f $clock.Elapsed.TotalMinutes, $process.ExitCode
            break
        }

        $sample = [pscustomobject]@{
            ElapsedMinutes = $clock.Elapsed.TotalMinutes
            CpuSeconds = $process.TotalProcessorTime.TotalSeconds
            WorkingSetMB = $process.WorkingSet64 / 1MB
            PrivateMB = $process.PrivateMemorySize64 / 1MB
            Handles = $process.HandleCount
            Threads = $process.Threads.Count
        }
        $measurements.Add($sample)
        Add-Content -LiteralPath $measurePath -Encoding utf8 -Value ([string]::Format($invariant, '{0:0.00},{1:0.000},{2:0.0},{3:0.0},{4},{5}',
                $sample.ElapsedMinutes, $sample.CpuSeconds, $sample.WorkingSetMB, $sample.PrivateMB, $sample.Handles, $sample.Threads))
        Write-Host ('  {0,7:N1} 分鐘  WS {1,7:N1} MB  Private {2,7:N1} MB  Handles {3,5}  Threads {4,3}' -f
            $sample.ElapsedMinutes, $sample.WorkingSetMB, $sample.PrivateMB, $sample.Handles, $sample.Threads)
        $nextMs += $MeasureIntervalSeconds * 1000.0
    }

    if ($null -eq $unexpectedExit) {
        if ($Target -eq 'App') {
            $null = $process.CloseMainWindow()
        }
        # Cli 時間到會自行停止；兩者都等待正常停止（補寫最後一筆、寫停止 Log）。
        if (-not $process.WaitForExit(120000)) {
            $failures.Add('時間到後 120 秒內沒有結束（已強制結束）')
        } elseif ($Target -eq 'Cli' -and $process.ExitCode -ne 0) {
            $failures.Add("Cli 結束碼 $($process.ExitCode)")
        }
    } else {
        $failures.Add($unexpectedExit)
    }
} finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        $null = $process.WaitForExit(10000)
    }
    $env:PERFORMANCEMONITOR_ROOT = $previousRoot
}
$elapsedMinutes = $clock.Elapsed.TotalMinutes

#endregion

#region 分析

function Get-Slope([double[]] $X, [double[]] $Y) {
    # 最小平方法斜率（y 對 x）。
    if ($X.Count -lt 2) { return $null }
    $mx = ($X | Measure-Object -Average).Average
    $my = ($Y | Measure-Object -Average).Average
    $num = 0.0
    $den = 0.0
    for ($i = 0; $i -lt $X.Count; $i++) {
        $num += ($X[$i] - $mx) * ($Y[$i] - $my)
        $den += ($X[$i] - $mx) * ($X[$i] - $mx)
    }
    if ($den -eq 0) { return $null }
    return $num / $den
}

$report = [System.Text.StringBuilder]::new()
function Add-Line([string] $Text = '') { $null = $report.AppendLine($Text) }

Add-Line '# 穩定性測試報告'
Add-Line
Add-Line '## 測試環境'
Add-Line
Add-Line '| 項目 | 內容 |'
Add-Line '|---|---|'
foreach ($key in $environment.Keys) { Add-Line "| $key | $($environment[$key]) |" }
Add-Line "| 設定 | $settingsSource（取樣 $sampleSeconds 秒、CSV $logSeconds 秒；資料根目錄 $root） |"
Add-Line ('| 測試時間 | 預定 {0} 分鐘，實際 {1:N1} 分鐘（開始 {2}） |' -f $DurationMinutes, $elapsedMinutes, $startedAt.ToString('yyyy-MM-dd HH:mm:ss', $invariant))
Add-Line

Add-Line '## 監控程式資源使用'
Add-Line
$warmupMinutes = 10.0
if ($measurements.Count -ge 2) {
    $logical = [System.Environment]::ProcessorCount
    $cpuPercents = for ($i = 1; $i -lt $measurements.Count; $i++) {
        $wall = ($measurements[$i].ElapsedMinutes - $measurements[$i - 1].ElapsedMinutes) * 60
        if ($wall -gt 0) { ($measurements[$i].CpuSeconds - $measurements[$i - 1].CpuSeconds) / $wall / $logical * 100 }
    }
    $steady = @($measurements | Where-Object { $_.ElapsedMinutes -ge $warmupMinutes })
    $privateSlope = $null
    $handleSlope = $null
    if ($steady.Count -ge 2 -and ($steady[-1].ElapsedMinutes - $steady[0].ElapsedMinutes) -ge 30) {
        $hours = [double[]]@($steady | ForEach-Object { $_.ElapsedMinutes / 60 })
        $privateSlope = Get-Slope $hours ([double[]]@($steady | ForEach-Object { $_.PrivateMB }))
        $handleSlope = Get-Slope $hours ([double[]]@($steady | ForEach-Object { [double]$_.Handles }))
    }
    $first = $measurements[0]
    $last = $measurements[-1]
    $na = 'N/A（10 分鐘後的資料少於 30 分鐘）'
    Add-Line '| 指標 | 開始 | 結束 | 最大 | 平均 | 趨勢（每小時，10 分鐘後） |'
    Add-Line '|---|---:|---:|---:|---:|---:|'
    Add-Line ('| CPU（占全部邏輯處理器 %） | | | {0:N2} | {1:N2} | |' -f ($cpuPercents | Measure-Object -Maximum).Maximum, ($cpuPercents | Measure-Object -Average).Average)
    Add-Line ('| Working Set（MB） | {0:N1} | {1:N1} | {2:N1} | {3:N1} | |' -f $first.WorkingSetMB, $last.WorkingSetMB,
        ($measurements | Measure-Object WorkingSetMB -Maximum).Maximum, ($measurements | Measure-Object WorkingSetMB -Average).Average)
    Add-Line ('| Private Bytes（MB） | {0:N1} | {1:N1} | {2:N1} | {3:N1} | {4} |' -f $first.PrivateMB, $last.PrivateMB,
        ($measurements | Measure-Object PrivateMB -Maximum).Maximum, ($measurements | Measure-Object PrivateMB -Average).Average,
        $(if ($null -ne $privateSlope) { '{0:+0.00;-0.00} MB' -f $privateSlope } else { $na }))
    Add-Line ('| Handles | {0} | {1} | {2} | {3:N0} | {4} |' -f $first.Handles, $last.Handles,
        ($measurements | Measure-Object Handles -Maximum).Maximum, ($measurements | Measure-Object Handles -Average).Average,
        $(if ($null -ne $handleSlope) { '{0:+0.0;-0.0}' -f $handleSlope } else { $na }))
    Add-Line ('| 執行緒 | {0} | {1} | {2} | {3:N0} | |' -f $first.Threads, $last.Threads,
        ($measurements | Measure-Object Threads -Maximum).Maximum, ($measurements | Measure-Object Threads -Average).Average)
    Add-Line
    Add-Line "量測 $($measurements.Count) 次，原始資料：monitor-resources.csv。不設 CPU／RAM 硬性上限，請依數值判讀。"
    if ($null -ne $privateSlope -and $privateSlope -gt 1 -and $last.PrivateMB -gt $steady[0].PrivateMB * 1.2) {
        $warnings.Add(('Private Bytes 持續上升（約 {0:N2} MB／小時），請檢查是否有未釋放資源' -f $privateSlope))
    }
    if ($null -ne $handleSlope -and $handleSlope -gt 10 -and $last.Handles -gt $steady[0].Handles * 1.2) {
        $warnings.Add(('Handle 數持續上升（約 {0:N1}／小時），可能有未關閉的物件' -f $handleSlope))
    }
} else {
    Add-Line '量測次數不足（少於 2 次），無法計算資源使用。'
}
Add-Line

Add-Line '## 資料完整性（CSV）'
Add-Line
function Import-Table([string] $Name) {
    $rows = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $dataDir -Filter "${Name}_v1_*.csv" -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $rows += @(Import-Csv -LiteralPath $file.FullName -Encoding utf8)
    }
    return , $rows
}

$appLines = @(Get-ChildItem -LiteralPath $logDir -Filter 'Application_*.log' -ErrorAction SilentlyContinue | Sort-Object Name |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Encoding utf8 } | Where-Object { $_ })
$errorLines = @(Get-ChildItem -LiteralPath $logDir -Filter 'Error_*.log' -ErrorAction SilentlyContinue | Sort-Object Name |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Encoding utf8 } | Where-Object { $_ })

function Get-LogTime([string] $Line) {
    [datetimeoffset]::ParseExact($Line.Substring(0, $Line.IndexOf(' ')), 'yyyy-MM-dd''T''HH:mm:ss.fffzzz', $invariant)
}
$starts = @($appLines | Where-Object { $_ -match '\[Monitor\] 監控開始' })
$stops = @($appLines | Where-Object { $_ -match '\[Monitor\] 監控正常停止' })
$faults = @($appLines | Where-Object { $_ -match '\[Monitor\] 監控異常結束' })

$system = Import-Table 'SystemMetrics'
if ($system.Count -eq 0) {
    $failures.Add('沒有任何 SystemMetrics 紀錄')
    Add-Line '沒有 SystemMetrics 紀錄。'
} else {
    $ids = [System.Collections.Generic.HashSet[string]]::new([string[]]@($system | ForEach-Object SampleId))
    $times = @($system | ForEach-Object { [datetimeoffset]::Parse($_.TimestampUtc, $invariant) } | Sort-Object)
    $gaps = 0
    for ($i = 1; $i -lt $times.Count; $i++) {
        if (($times[$i] - $times[$i - 1]).TotalSeconds -gt $logSeconds * 1.5) { $gaps++ }
    }
    # 預期筆數：每次監控 ⌈時長 ÷ 間隔⌉ ＋ 1（與報告相同），容許每次監控 ±1 筆。
    $expected = 0
    $sessions = [math]::Min($starts.Count, $stops.Count)
    for ($i = 0; $i -lt $sessions; $i++) {
        $seconds = ((Get-LogTime $stops[$i]) - (Get-LogTime $starts[$i])).TotalSeconds
        $expected += [math]::Ceiling($seconds / $logSeconds - 1e-9) + 1
    }
    $missing = [math]::Max(0, $expected - $system.Count)
    $tolerance = [math]::Max($sessions, 0) + [math]::Floor($expected * 0.01)
    $duplicates = $system.Count - $ids.Count
    $overruns = @($system | Where-Object { $_.LoopOverrun -eq 'True' }).Count
    $okButEmpty = @($system | Where-Object { ($_.CpuStatus -eq 'OK' -and $_.CpuUsagePercent -eq '') -or ($_.MemoryStatus -eq 'OK' -and $_.MemoryUsagePercent -eq '') }).Count

    Add-Line '| 檢查 | 結果 |'
    Add-Line '|---|---|'
    Add-Line "| SystemMetrics 筆數 | $($system.Count)（預期 $expected；容許差 $tolerance） |"
    Add-Line "| 重複 SampleId | $duplicates |"
    Add-Line "| 間隔超過 1.5 倍紀錄週期的缺口 | $gaps |"
    Add-Line "| LoopOverrun 筆數 | $overruns |"
    Add-Line "| 狀態 OK 但數值空白 | $okButEmpty |"
    foreach ($name in @('DiskMetrics', 'DiskIoMetrics', 'GpuMetrics', 'NetworkMetrics', 'ProcessMetrics', 'TemperatureMetrics')) {
        $rows = Import-Table $name
        $orphans = @($rows | Where-Object { -not $ids.Contains($_.SampleId) }).Count
        Add-Line "| $name 筆數／找不到對應 SampleId | $($rows.Count) ／ $orphans |"
        if ($orphans -gt 0) { $failures.Add("$name 有 $orphans 筆找不到對應的 SystemMetrics SampleId") }
    }
    if ($duplicates -gt 0) { $failures.Add("SystemMetrics 有 $duplicates 筆重複 SampleId") }
    if ($okButEmpty -gt 0) { $failures.Add("有 $okButEmpty 筆狀態為 OK 但數值空白") }
    if ($missing -gt $tolerance) { $failures.Add(('CSV 缺失 {0} 筆（預期 {1}，超過容許 {2}）' -f $missing, $expected, $tolerance)) }
    if ($gaps -gt 0) { $warnings.Add("CSV 紀錄有 $gaps 處間隔超過 1.5 倍紀錄週期（系統休眠或程式停頓）") }
    if ($overruns -gt 0) { $warnings.Add("有 $overruns 筆 LoopOverrun（取樣超過週期）") }
}
Add-Line

Add-Line '## Log'
Add-Line
$lost = 0
foreach ($line in $stops) {
    if ($line -match '遺失 (\d+) 列') { $lost += [int]$Matches[1] }
}
Add-Line "- Application Log：$($appLines.Count) 行；監控開始 $($starts.Count)、正常停止 $($stops.Count)、異常結束 $($faults.Count)；告警事件 $(@($appLines | Where-Object { $_ -match '\[Alert\]' }).Count) 筆；LoopOverrun 紀錄 $(@($appLines | Where-Object { $_ -match 'LoopOverrun' }).Count) 筆"
Add-Line "- Error Log：$($errorLines.Count) 行（ERROR $(@($errorLines | Where-Object { $_ -match '\[ERROR\]' }).Count)、WARN $(@($errorLines | Where-Object { $_ -match '\[WARN\]' }).Count)）"
Add-Line "- CSV 遺失列（停止 Log）：$lost"
if ($stops.Count -eq 0) { $failures.Add('Application Log 沒有「監控正常停止」') }
if ($faults.Count -gt 0) { $failures.Add("Application Log 有 $($faults.Count) 筆「監控異常結束」") }
if ($lost -gt 0) { $failures.Add("CSV 遺失 $lost 列") }
$errorCount = @($errorLines | Where-Object { $_ -match '\[ERROR\]' }).Count
if ($errorCount -gt 0) { $warnings.Add("Error Log 有 $errorCount 筆 ERROR（請檢查是否為硬體不支援以外的錯誤）") }
Add-Line

# HTML 報告（需要 Cli）。
if ($Cli -and $system.Count -gt 0) {
    $env:PERFORMANCEMONITOR_ROOT = $root
    try {
        $from = $startedAt.ToString('yyyy-MM-dd', $invariant)
        $to = (Get-Date).ToString('yyyy-MM-dd', $invariant)
        $html = Join-Path $OutputDirectory 'report.html'
        $null = & $Cli report --from $from --to $to --out $html
        Add-Line "- HTML 報告：report.html（主控台 report，結束碼 $LASTEXITCODE）"
    } finally {
        $env:PERFORMANCEMONITOR_ROOT = $previousRoot
    }
}
Add-Line

Add-Line '## 結論'
Add-Line
if ($failures.Count -eq 0 -and $warnings.Count -eq 0) {
    Add-Line '**通過**，未發現異常（資源使用請依上表數值判讀）。'
} else {
    Add-Line $(if ($failures.Count -eq 0) { '**通過**（有警告）。' } else { '**失敗**。' })
    Add-Line
    foreach ($f in $failures) { Add-Line "- 失敗：$f" }
    foreach ($w in $warnings) { Add-Line "- 警告：$w" }
}

$reportPath = Join-Path $OutputDirectory 'stability-report.md'
Set-Content -LiteralPath $reportPath -Value $report.ToString() -Encoding utf8
Write-Host ''
Write-Host "測試報告：$reportPath"
foreach ($f in $failures) { Write-Host "  [失敗] $f" -ForegroundColor Red }
foreach ($w in $warnings) { Write-Host "  [警告] $w" -ForegroundColor Yellow }
if ($failures.Count -gt 0) { exit 1 }
Write-Host '  通過'

#endregion
