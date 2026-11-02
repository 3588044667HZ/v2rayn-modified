<#
.SYNOPSIS
    将 v2rayN 编译打包为 Windows 版本。

.DESCRIPTION
    默认编译 WPF 版(v2rayN.csproj)。可切换 Avalonia 桌面版、arm64、自包含发布。
    -WithCore 会从 2dust/v2rayN-core-bin 下载核心(xray/sing-box/mihomo/geo)并合成发布 zip。

.EXAMPLE
    ./build-windows.ps1

.EXAMPLE
    ./build-windows.ps1 -SelfContained -WithCore

.EXAMPLE
    ./build-windows.ps1 -Arch win-arm64 -SelfContained

.EXAMPLE
    ./build-windows.ps1 -UI Avalonia -SelfContained
#>
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Arch = 'win-x64',

    [ValidateSet('Wpf', 'Avalonia')]
    [string]$UI = 'Wpf',

    # 自包含(不依赖系统安装 .NET 运行时)，体积更大
    [switch]$SelfContained,

    # 下载核心二进制并合成最终 zip
    [switch]$WithCore,

    [string]$Configuration = 'Release',

    # 自定义发布目录，留空使用 v2rayN/Release/<arch>
    [string]$Output
)
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw '未找到 dotnet，请先安装 .NET 8 SDK。' }

$OutputArch = if ($Arch -eq 'win-arm64') { 'windows-arm64' } else { 'windows-64' }
if ($UI -eq 'Avalonia') { $OutputArch = "$OutputArch-desktop" }

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $root "v2rayN/Release/$OutputArch"
}

$proj = if ($UI -eq 'Avalonia') {
    Join-Path $root 'v2rayN/v2rayN.Desktop/v2rayN.Desktop.csproj'
} else {
    Join-Path $root 'v2rayN/v2rayN/v2rayN.csproj'
}
$amazProj = Join-Path $root 'v2rayN/AmazTool/AmazTool.csproj'

if (-not (Test-Path $proj)) { throw "找不到项目文件: $proj" }

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Output | Out-Null

$selfArg = if ($SelfContained) { 'true' } else { 'false' }
$trimArg = if ($SelfContained) { 'true' } else { 'false' }

Write-Host "==> 编译 $UI ($Arch, self-contained=$selfArg) => $Output" -ForegroundColor Cyan
Push-Location (Join-Path $root 'v2rayN')
try {
    dotnet publish $proj -c $Configuration -r $Arch --self-contained=$selfArg -p:EnableWindowsTargeting=true -o $Output
    if ($LASTEXITCODE -ne 0) { throw "主程序发布失败" }

    dotnet publish $amazProj -c $Configuration -r $Arch --self-contained=$selfArg -p:EnableWindowsTargeting=true -p:PublishTrimmed=$trimArg -o $Output
    if ($LASTEXITCODE -ne 0) { throw "AmazTool 发布失败" }
}
finally {
    Pop-Location
}

Write-Host "==> 编译完成: $Output" -ForegroundColor Green

if ($WithCore) {
    $zipName = "v2rayN-$OutputArch.zip"
    $coreUrl = "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/$zipName"
    $tmpRoot = Join-Path $env:TEMP "v2rayN-core-$OutputArch"
    $coreZip = Join-Path $env:TEMP $zipName

    Write-Host "==> 下载核心: $coreUrl" -ForegroundColor Cyan
    if (Test-Path $tmpRoot) { Remove-Item $tmpRoot -Recurse -Force }
    Invoke-WebRequest -Uri $coreUrl -OutFile $coreZip -UseBasicParsing
    Expand-Archive -Path $coreZip -DestinationPath $tmpRoot -Force

    $coreDir = Join-Path $tmpRoot "v2rayN-$OutputArch"
    if (-not (Test-Path $coreDir)) {
        # 兜底：若压缩包内目录名不同，取第一个子目录
        $coreDir = (Get-ChildItem $tmpRoot -Directory | Select-Object -First 1).FullName
    }

    # 用发布产物覆盖到核心目录，保留核心文件
    Copy-Item -Path (Join-Path $Output '*') -Destination $coreDir -Recurse -Force

    $finalZip = Join-Path $root $zipName
    if (Test-Path $finalZip) { Remove-Item $finalZip -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $coreDir, $finalZip,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $true
    )
    Write-Host "==> 已生成: $finalZip" -ForegroundColor Green
}
else {
    Write-Host "提示: 未加 -WithCore，输出目录仅含主程序，缺少核心二进制(xray/sing-box 等)。" -ForegroundColor Yellow
}
