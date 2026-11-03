# 发布到 GitHub —— 本机不需要安装 Git for Windows（用 DSH 自带的 git）
# 用法：双击同目录的「发布到GitHub.cmd」
$ErrorActionPreference = 'Continue'
$proj = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $proj
$remote = 'https://github.com/MengzhongYiyu/PDFBatchPrint.git'

function Find-GitExe {
  $cands = @(
    (Join-Path $env:ProgramFiles 'Git\cmd\git.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Git\cmd\git.exe'),
    (Join-Path $env:LocalAppData 'Programs\Git\cmd\git.exe')
  )
  $w = Get-Command git -ErrorAction SilentlyContinue
  if ($w) { $cands += $w.Source }
  Get-ChildItem (Join-Path $env:USERPROFILE '.dsh\profiles\*') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $cands += (Join-Path $_.FullName 'node_modules\@dsh-ops\bash-win32-x64\ucrt64\bin\git.exe')
  }
  foreach ($c in $cands) { if ($c -and (Test-Path $c)) { return $c } }
  return $null
}

Write-Host '============================================' -ForegroundColor Cyan
Write-Host '   把 PDF批量打印工具 推送到 GitHub' -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan
Write-Host ''

$git = Find-GitExe
if (-not $git) {
  Write-Host '[!] 找不到 git.exe。可安装 Git for Windows: https://git-scm.com/download/win' -ForegroundColor Red
  Read-Host '按回车退出'
  exit 1
}
Write-Host ('使用 git: ' + $git)
& $git --version
Write-Host ''

Write-Host '[检查远程仓库]' -ForegroundColor Yellow
$existing = & $git remote get-url origin 2>$null
if ($LASTEXITCODE -ne 0 -or -not $existing) {
  Write-Host ('  添加 origin = ' + $remote)
  & $git remote add origin $remote
} else {
  Write-Host ('  已有 origin: ' + $existing)
}
Write-Host ''

Write-Host '[开始推送]' -ForegroundColor Yellow
Write-Host '  第一次会要你输入两次：'
Write-Host '    Username for ''https://github.com'':  你的 GitHub 用户名'
Write-Host '    Password for ''https://...'':          粘贴访问令牌 PAT'
Write-Host '    （令牌不是登录密码；粘贴时屏幕上不显示字符，是正常的）'
Write-Host ''
& $git -c credential.helper= push -u origin main
$code = $LASTEXITCODE
Write-Host ''

if ($code -ne 0) {
  Write-Host '[!] 推送失败。按顺序检查：' -ForegroundColor Red
  Write-Host '    1. 网页上是否已创建空仓库 https://github.com/MengzhongYiyu/PDFBatchPrint'
  Write-Host '       （建仓时不要勾 Add a README / .gitignore / license）'
  Write-Host '    2. 令牌是否勾了 repo 权限、是否复制完整（ghp_ 或 github_pat_ 开头）'
  Write-Host '    3. 密码处是否粘成了 GitHub 登录密码 —— 必须是令牌'
  Write-Host '    4. 网络抖动就再运行一次本脚本'
} else {
  Write-Host '[OK] 推送成功！' -ForegroundColor Green
  Write-Host '     打开 https://github.com/MengzhongYiyu/PDFBatchPrint 看看' -ForegroundColor Green
}
Write-Host ''
Read-Host '按回车关闭窗口'
