# PowerTray 一键构建：生成图标 -> dotnet publish -> Inno Setup 安装包
[CmdletBinding()]
param(
    [string]$Version = '1.0.0',   # 版本号（写入程序集与安装包）
    [switch]$SkipInstaller,       # 只构建可执行文件
    [switch]$Run                  # 构建完成后启动试运行
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

Write-Host '==> 生成应用图标' -ForegroundColor Cyan
& (Join-Path $root 'tools\Generate-Icon.ps1')

Write-Host '==> dotnet publish (Release, win-x64, self-contained)' -ForegroundColor Cyan
$publishDir = Join-Path $root 'dist\publish'
& $dotnet publish (Join-Path $root 'src\PowerTray\PowerTray.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败（exit $LASTEXITCODE）" }

if (-not $SkipInstaller) {
    Write-Host '==> Inno Setup 打包' -ForegroundColor Cyan
    $iscc = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) { throw '未找到 ISCC.exe（Inno Setup 6）' }
    & $iscc "/DMyAppVersion=$Version" (Join-Path $root 'installer\PowerTray.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC 打包失败（exit $LASTEXITCODE）" }
}

Write-Host ''
Write-Host "构建完成（v$Version）：" -ForegroundColor Green
Write-Host "  可执行文件目录 : $publishDir"
if (-not $SkipInstaller) {
    Write-Host "  安装包         : $(Join-Path $root "dist\PowerTray-Setup-$Version.exe")"
}

if ($Run) {
    Start-Process (Join-Path $publishDir 'PowerTray.exe')
}
