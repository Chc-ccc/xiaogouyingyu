<#
  本地打包兜底脚本（仅当本机已装 .NET 8 SDK + Inno Setup 时使用；家长无需本地环境）。
  云端出包失败时可用本脚本在本机自行编译并打包作为兜底，不作为常规交付路径。

  用法：
    powershell -ExecutionPolicy Bypass -File scripts/build-local.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src/XiaoGouYingYu.csproj'
$out = Join-Path $root 'artifacts/publish'

Write-Host '1) 检查环境...'
dotnet --version | Out-Null
if ($LASTEXITCODE -ne 0) { throw '未找到 .NET 8 SDK' }

Write-Host '2) 读取版本号...'
$xml = [xml](Get-Content -Raw $src)
$version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = '1.0.0' }

Write-Host '3) 发布单文件 exe...'
New-Item -ItemType Directory -Force -Path $out | Out-Null
dotnet publish $src -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }

Write-Host '4) 定位 ISCC...'
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "$env:ProgramFiles\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { throw '未找到 Inno Setup 6 的 ISCC.exe，请先安装（家长无需此步）' }

Write-Host '5) 打包 Setup.exe...'
$iss = Join-Path $root '.github/installer/setup.iss'
& $iscc "/DMyAppVersion=$version" $iss
if ($LASTEXITCODE -ne 0) { throw 'ISCC 编译失败' }

Write-Host '完成。安装包在 artifacts 目录。'
Get-ChildItem (Join-Path $root 'artifacts') -Filter '*.exe' | Select-Object Name, Length
