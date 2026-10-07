# uninstall-autostart.ps1
# 删除 LocalService 的登录自启项；加 -Stop 同时结束正在运行的 LocalService
# （其拉起的 demo.exe 在 Job Object 中，随 LocalService 结束被系统一并结束）。
param([switch]$Stop)
$ErrorActionPreference = 'Stop'

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$name = 'JsfHandlerLocalService'
if (Get-ItemProperty -Path $runKey -Name $name -ErrorAction SilentlyContinue) {
  Remove-ItemProperty -Path $runKey -Name $name
  Write-Host "已删除登录自启: $runKey\$name"
} else {
  Write-Host '未找到登录自启项，无需删除'
}

if ($Stop) {
  # LocalService 无窗口只能强制结束；demo.exe 由 Job Object 连带结束，不按名字杀（避免误杀扩展拉起的 demo.exe）
  Get-Process -Name 'LocalService' -ErrorAction SilentlyContinue | Stop-Process -Force
  Write-Host '已结束 LocalService'
}
