# install-autostart.ps1
# 让 LocalService 在当前用户登录时自启：写 HKCU\Software\Microsoft\Windows\CurrentVersion\Run。
# 不注册为 Windows 服务：服务运行在 Session 0，拉起的 demo.exe 窗口用户看不到（见 docs/webapi-migration.md §5.5）。
# 写 HKCU 无需管理员权限。
param(
  [string]$ExePath,
  [switch]$StartNow   # 写完后立即启动一次（单实例，已在运行则新进程自动退出）
)
$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ExePath) { $ExePath = Join-Path (Split-Path -Parent $here) 'dist\LocalService.exe' }
if (-not (Test-Path $ExePath)) { throw "找不到 $ExePath，请先运行 publish.ps1" }
$ExePath = (Resolve-Path $ExePath).Path

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$name = 'JsfHandlerLocalService'
Set-ItemProperty -Path $runKey -Name $name -Value "`"$ExePath`""
Write-Host "已写入登录自启: $runKey\$name -> `"$ExePath`""

if ($StartNow) {
  Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath)
  Write-Host '已启动 LocalService'
}
