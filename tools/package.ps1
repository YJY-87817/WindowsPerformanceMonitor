#Requires -Version 7.2
<#
.SYNOPSIS
  發行打包（決策 0006、0009）：兩個 self-contained 單一檔案 exe（啟用壓縮）→ zip ＋ SHA-256。

.DESCRIPTION
  1. dotnet publish App 與 Cli（win-x64、self-contained、PublishSingleFile、EnableCompressionInSingleFile、
     IncludeNativeLibrariesForSelfExtract；不輸出 .pdb）到 <PublishDirectory>/App、<PublishDirectory>/Cli。
  2. 組成 zip：PerformanceMonitor.exe、PerformanceMonitor.Cli.exe、使用說明.md、CHANGELOG.md、THIRD-PARTY-NOTICES.md、settings.example.json。
  3. 產生 SHA256SUMS.txt（zip 的 SHA-256）。
  CI（Build Check 與 Smoke Test 使用同一份發行檔）與 release workflow 都呼叫本腳本。

.PARAMETER Configuration
  建置設定，預設 Release。

.PARAMETER PublishDirectory
  發行輸出資料夾，預設 publish。

.PARAMETER OutputDirectory
  zip 與 SHA256SUMS.txt 的輸出資料夾，預設 artifacts。
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [string] $PublishDirectory = 'publish',
    [string] $OutputDirectory = 'artifacts'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    [xml] $props = Get-Content -LiteralPath 'Directory.Build.props' -Raw
    $version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
    if (-not $version) { throw 'Directory.Build.props 沒有 Version' }

    $publishArgs = @(
        '-c', $Configuration, '-r', 'win-x64', '--self-contained',
        '-p:PublishSingleFile=true', '-p:EnableCompressionInSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=none', '-p:GenerateDocumentationFile=false'
    )
    foreach ($project in @(@{ Path = 'src/PerformanceMonitor.App'; Out = 'App' }, @{ Path = 'src/PerformanceMonitor.Cli'; Out = 'Cli' })) {
        $out = Join-Path $PublishDirectory $project.Out
        if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
        & dotnet publish $project.Path @publishArgs -o $out
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish $($project.Path) 失敗（結束碼 $LASTEXITCODE）" }
    }

    $exes = @((Join-Path $PublishDirectory 'App/PerformanceMonitor.exe'), (Join-Path $PublishDirectory 'Cli/PerformanceMonitor.Cli.exe'))
    foreach ($exe in $exes) {
        if (-not (Test-Path -LiteralPath $exe)) { throw "找不到發行檔：$exe" }
    }
    $extra = @(Get-ChildItem -LiteralPath (Join-Path $PublishDirectory 'App'), (Join-Path $PublishDirectory 'Cli') -File |
        Where-Object { $_.Extension -ne '.exe' })
    if ($extra.Count -gt 0) {
        throw "單一檔案發行仍有其他檔案：$(($extra | ForEach-Object Name) -join '、')"
    }

    $name = "WindowsPerformanceMonitor-$version-win-x64"
    $null = New-Item -ItemType Directory -Path $OutputDirectory -Force
    $staging = Join-Path $OutputDirectory $name
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $staging
    Copy-Item -LiteralPath $exes -Destination $staging
    Copy-Item -LiteralPath 'docs/使用說明.md', 'CHANGELOG.md', 'THIRD-PARTY-NOTICES.md', 'docs/settings.example.json' -Destination $staging
    $zip = Join-Path $OutputDirectory "$name.zip"
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal
    Remove-Item -LiteralPath $staging -Recurse -Force

    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Value "$hash  $name.zip" -Encoding utf8NoBOM

    foreach ($exe in $exes) {
        Write-Host ('  {0}：{1:N1} MB' -f (Split-Path $exe -Leaf), ((Get-Item -LiteralPath $exe).Length / 1MB))
    }
    Write-Host ('  {0}：{1:N1} MB，SHA-256 {2}' -f (Split-Path $zip -Leaf), ((Get-Item -LiteralPath $zip).Length / 1MB), $hash)
} finally {
    Pop-Location
}
