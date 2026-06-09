# register-host.ps1
# 1) 发布出单文件 demo.exe（放在本目录，与 com.demo.host.json 同级，对应 path: ".\\demo.exe"）
# 2) 把 native messaging host 注册到 Edge（当前用户 HKCU）
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$hostName = 'com.demo.host'
$manifest = Join-Path $here 'com.demo.host.json'
$csproj   = Join-Path $here 'DemoHost.csproj'

Write-Host '发布 demo.exe（框架依赖单文件，依赖已安装的 .NET 10 运行时）...'
# 不要用 -o 把输出目录设成项目源码目录（native-host），否则会触发 CSC error CS5001（找不到 Main）。
# 改为发布到默认的 bin\...\publish\，再把单文件 demo.exe 复制回本目录（与 json 同级）。
dotnet publish $csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }

$published = Get-ChildItem -Path (Join-Path $here 'bin') -Filter 'demo.exe' -Recurse -ErrorAction SilentlyContinue |
  Where-Object { $_.DirectoryName -like '*\publish' } | Select-Object -First 1
if (-not $published) { throw '未在 bin 下找到发布的 demo.exe' }

$exe = Join-Path $here 'demo.exe'
Copy-Item $published.FullName $exe -Force
Write-Host "已生成: $exe"

# 写注册表：Edge 的 NativeMessagingHosts，默认值指向清单的绝对路径
$regPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$hostName"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name '(default)' -Value $manifest
Write-Host "已注册: $regPath -> $manifest"

Write-Host ''
Write-Host '提醒：com.demo.host.json 的 allowed_origins 必须填真实扩展 ID（chrome-extension://<ID>/），'
Write-Host '      即 edge://extensions 里加载 demo extension 后显示的那个 ID。'
