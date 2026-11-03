# 发布到 GitHub —— 不需要安装 Git for Windows（用系统或 DSH 自带的 git）
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

Write-Host '[1/4] 检查远程仓库' -ForegroundColor Yellow
$existing = & $git remote get-url origin 2>$null
if ($LASTEXITCODE -ne 0 -or -not $existing) {
  Write-Host ('  添加 origin = ' + $remote)
  & $git remote add origin $remote
} else {
  Write-Host ('  已有 origin: ' + $existing)
}
Write-Host ''

Write-Host '[2/4] 这次要推送的提交' -ForegroundColor Yellow
& $git log --oneline -3
Write-Host ''

Write-Host '[3/4] 输入 GitHub 账号' -ForegroundColor Yellow
Write-Host '  用户名就是你的 GitHub 账号名（不是邮箱）'
$user = Read-Host '  GitHub 用户名'
if (-not $user) { Write-Host '[!] 用户名为空，退出。' -ForegroundColor Red; Read-Host '按回车退出'; exit 1 }
Write-Host '  令牌：在 https://github.com/settings/tokens 生成，勾 repo 权限'
Write-Host '  （粘贴时屏幕上不显示字符，属于正常现象）'
# 真实控制台里用隐藏输入；被重定向时（自动化/管道）回退成普通输入，避免挂住
if ([Console]::IsInputRedirected) {
  Write-Host '  （当前不是交互窗口，令牌会显示在屏幕上）' -ForegroundColor DarkGray
  $token = (Read-Host '  粘贴访问令牌 PAT').Trim()
} else {
  $sec = Read-Host '  粘贴访问令牌 PAT' -AsSecureString
  $token = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
}
if (-not $token) { Write-Host '[!] 令牌为空，退出。' -ForegroundColor Red; Read-Host '按回车退出'; exit 1 }
Write-Host ''

$tmp = Join-Path $env:TEMP ('pdfbatch_gitcred_' + [guid]::NewGuid().ToString('N') + '.txt')
try {
  # 只在本次命令生效的临时凭据文件，推送完立即删除
  $line = 'https://' + [uri]::EscapeDataString($user) + ':' + [uri]::EscapeDataString($token) + '@github.com'
  [System.IO.File]::WriteAllText($tmp, $line)
  Write-Host '[4/4] 推送中（约 400KB，通常几秒）...' -ForegroundColor Yellow
  $env:GIT_TERMINAL_PROMPT = '0'
  & $git -c ('credential.helper=store --file=' + $tmp.Replace('\', '/')) push -u origin main
  $code = $LASTEXITCODE
} finally {
  if (Test-Path $tmp) { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
}
Write-Host ''
if ($code -ne 0) {
  Write-Host '[!] 推送失败。按顺序检查：' -ForegroundColor Red
  Write-Host '    1. 网页上是否已创建空仓库 https://github.com/MengzhongYiyu/PDFBatchPrint'
  Write-Host '       （建仓时不要勾 Add a README / .gitignore / license）'
  Write-Host '    2. 令牌是否勾了 repo 权限、是否复制完整（ghp_ 或 github_pat_ 开头）'
  Write-Host '    3. 用户名写成了邮箱？要填 GitHub 账号名'
  Write-Host '    4. 令牌过期了就在同一页面重新生成一个'
  Write-Host '    5. 网络抖动就再运行一次本脚本'
} else {
  Write-Host '[OK] 推送成功！' -ForegroundColor Green
  Write-Host '     打开 https://github.com/MengzhongYiyu/PDFBatchPrint 看看' -ForegroundColor Green
}
Write-Host ''
Read-Host '按回车关闭窗口'
