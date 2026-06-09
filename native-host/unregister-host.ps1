# unregister-host.ps1
# 从 Edge（当前用户 HKCU）注销 native messaging host。
$ErrorActionPreference = 'Stop'

$hostName = 'com.demo.host'
$regPath = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$hostName"

if (Test-Path $regPath) {
    Remove-Item -Path $regPath -Recurse -Force
    Write-Host "已删除注册表项: $regPath"
} else {
    Write-Host "注册表项不存在: $regPath"
}
