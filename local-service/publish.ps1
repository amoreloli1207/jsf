# publish.ps1
# 把 LocalService.exe 与 demo.exe 一起发布到仓库根的 dist\（框架依赖单文件，依赖本机已装的 .NET 10）。
# dist\ 即 PoC 直接启动、以及登录自启（install-autostart.ps1）所指向的部署目录。
# LocalService 的 Host:ExePath 默认为 "demo.exe"（相对程序目录），故二者必须同目录。
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Split-Path -Parent $here
$dist = Join-Path $repo 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host '发布 LocalService.exe ...'
dotnet publish (Join-Path $here 'LocalService.csproj') -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o $dist
if ($LASTEXITCODE -ne 0) { throw 'LocalService 发布失败' }

Write-Host '发布 demo.exe ...'
# 不能 -o 到 native-host 源码目录（CS5001），先发布到默认 publish 目录再复制
$nativeHost = Join-Path $repo 'native-host'
dotnet publish (Join-Path $nativeHost 'DemoHost.csproj') -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'demo.exe 发布失败' }

$published = Get-ChildItem -Path (Join-Path $nativeHost 'bin') -Filter 'demo.exe' -Recurse |
  Where-Object { $_.DirectoryName -like '*\publish' } | Select-Object -First 1
if (-not $published) { throw '未在 native-host\bin 下找到发布的 demo.exe' }
Copy-Item $published.FullName (Join-Path $dist 'demo.exe') -Force

Write-Host ''
Write-Host "已发布到: $dist"
Write-Host '  生产启动: dist\LocalService.exe'
Write-Host '  开发启动（放行 file:// 与 localhost:8080）: dist\LocalService.exe --environment Development'
