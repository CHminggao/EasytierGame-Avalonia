<#
.SYNOPSIS
    构建 easytier-game（Avalonia 版）绿色发布包。

.DESCRIPTION
    与 WPF 版 publish.ps1 流程一致：
      1. dotnet publish（self-contained 单文件，解压即用）
      2. 强制覆盖拷贝 assets/easytier -> publish-avalonia/easytier
      3. 校验关键文件齐全（easytier-core.exe / easytier-cli.exe / wintun.dll 等）
      4. 打包成 release/easytier-game_avalonia_windows_x86_64_<version>.zip

.PARAMETER Version
    版本号，默认取 csproj 里的 Version。

.PARAMETER SkipPublish
    跳过 publish，只重新打 zip。
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$appProject = Join-Path $root 'src/EasytierGame.Avalonia/EasytierGame.Avalonia.csproj'
$publishDir = Join-Path $root 'publish-avalonia'
$releaseDir = Join-Path $root 'release'
$assetsDir = Join-Path $root 'assets/easytier'
$targetEasytier = Join-Path $publishDir 'easytier'

$requiredFiles = @(
    'easytier-core.exe',
    'easytier-cli.exe',
    'wintun.dll',
    'config_template.json'
)

# ---------------------------------------------------------------
# 版本号
# ---------------------------------------------------------------
if (-not $Version) {
    [xml]$csproj = Get-Content $appProject
    $Version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
}
if (-not $Version) { $Version = '0.0.0' }
Write-Host "版本: $Version (Avalonia 版)" -ForegroundColor Cyan

# ---------------------------------------------------------------
# 1. publish
# ---------------------------------------------------------------
if (-not $SkipPublish) {
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    Write-Host '正在发布（self-contained 单文件，win-x64）...' -ForegroundColor Cyan
    dotnet publish $appProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }
}

# ---------------------------------------------------------------
# 2. 拷贝运行时资源（总是覆盖）
# ---------------------------------------------------------------
if (-not (Test-Path $assetsDir)) { throw "找不到资源目录: $assetsDir" }

Write-Host '拷贝 easytier 运行资源...' -ForegroundColor Cyan
if (-not (Test-Path $targetEasytier)) {
    New-Item -ItemType Directory -Path $targetEasytier -Force | Out-Null
}
Copy-Item (Join-Path $assetsDir '*') $targetEasytier -Recurse -Force

# ---------------------------------------------------------------
# 3. 清理运行期产物
# ---------------------------------------------------------------
foreach ($dir in @('logs', 'guiLogs', 'cache')) {
    $p = Join-Path $targetEasytier $dir
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
foreach ($file in @('.client.pid', '.server.pid')) {
    $p = Join-Path $targetEasytier $file
    if (Test-Path $p) { Remove-Item $p -Force }
}
$rootSettings = Join-Path $publishDir 'ui-settings.json'
if (Test-Path $rootSettings) { Remove-Item $rootSettings -Force }

$helpSource = Join-Path $assetsDir '帮助.txt'
if (Test-Path $helpSource) {
    Copy-Item $helpSource (Join-Path $publishDir '帮助.txt') -Force
}

# ---------------------------------------------------------------
# 4. 完整性校验
# ---------------------------------------------------------------
Write-Host '校验发布完整性...' -ForegroundColor Cyan
$missing = @()
foreach ($name in $requiredFiles) {
    if (-not (Test-Path (Join-Path $targetEasytier $name))) { $missing += $name }
}

if ($missing.Count -gt 0) {
    Write-Host ''
    Write-Host '[FAIL] 发布不完整，以下文件缺失：' -ForegroundColor Red
    foreach ($m in $missing) { Write-Host "   - easytier/$m" -ForegroundColor Red }
    Write-Host ''
    Write-Host '常见原因：csproj 里缺少 ExcludeFromSingleFile="true"，' -ForegroundColor Yellow
    Write-Host '导致这些文件被 PublishSingleFile 吞进了 exe。' -ForegroundColor Yellow
    throw '发布完整性校验失败'
}

$exePath = Join-Path $publishDir 'easytier-game.exe'
$exeMb = [math]::Round((Get-Item $exePath).Length / 1MB, 1)
$coreMb = [math]::Round((Get-Item (Join-Path $targetEasytier 'easytier-core.exe')).Length / 1MB, 1)
Write-Host "  [OK] 关键文件齐全（exe ${exeMb}MB，easytier-core ${coreMb}MB）" -ForegroundColor Green

# ---------------------------------------------------------------
# 5. 打包
# ---------------------------------------------------------------
if (-not (Test-Path $releaseDir)) { New-Item -ItemType Directory -Path $releaseDir | Out-Null }
$zipPath = Join-Path $releaseDir "easytier-game_avalonia_windows_x86_64_$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "正在打包: $zipPath" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal

$zipMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "完成: $zipPath (${zipMb} MB)" -ForegroundColor Green
