$ErrorActionPreference = 'Stop'
# 在 Server1 上以管理员身份运行：清空卡住的打印任务并重启打印服务
Write-Host '=== 1) 列出当前任务 ==='
Get-PrintJob -PrinterName 'HP MFP M433 PCL 6' -ErrorAction SilentlyContinue |
  Select-Object Id,DocumentName,JobStatus,SubmittedTime | Format-Table -AutoSize
Write-Host '=== 2) 尝试取消任务 ==='
Get-PrintJob -PrinterName 'HP MFP M433 PCL 6' -ErrorAction SilentlyContinue | ForEach-Object {
  try { Remove-PrintJob -InputObject $_ -ErrorAction Stop; "已取消: $($_.Id)" } catch { "取消失败: $($_.Id) $($_.Exception.Message)" }
}
Write-Host '=== 3) 重启打印服务 ==='
Stop-Service Spooler -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$spool = Join-Path $env:WINDIR 'System32\spool\PRINTERS'
Get-ChildItem $spool -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt (Get-Date).AddYears(-1) } | Remove-Item -Force -ErrorAction SilentlyContinue
Start-Service Spooler
Start-Sleep -Seconds 2
Write-Host '=== 4) 结果 ==='
Get-PrintJob -PrinterName 'HP MFP M433 PCL 6' -ErrorAction SilentlyContinue | Select-Object Id,JobStatus | Format-Table -AutoSize
(Get-Printer -Name 'HP MFP M433 PCL 6').JobCount
Write-Host '完成。如果打印机仍然没有动作，请检查打印机电源与 USB 线。'
Read-Host '按回车退出'