# 把便携包上传到 GitHub Releases（不需要装 gh / Git for Windows）
# 用法：双击同目录的「发布便携包到Release.cmd」
$ErrorActionPreference = 'Continue'
$proj = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $proj

$owner = 'MengzhongYiyu'
$repo  = 'PDFBatchPrint'
$tag   = 'v1.4.1'
$title = 'v1.4.1 便携版（免装 Python，解压即用）'
$zipLocal = Join-Path $proj 'dist\PDF批量打印工具_便携版_v1.4.1.zip'
$assetName = 'PDFBatchPrint_v1.4.1_portable.zip'

$body = @'
## PDF批量打印工具 v1.4.1 便携版

解压即用，**不需要安装 Python**（内置 Python 3.12 + PyMuPDF + Pillow）。

### 用法
1. 下载并解压 `PDFBatchPrint_v1.4.1_portable.zip`，得到 `PDF批量打印工具\` 目录；
2. 建议放到本地磁盘（例如 `D:\PDF批量打印工具`），不要直接在 U 盘/网络盘里跑；
3. 双击 `PdfBatchPrint.exe`。首次使用在界面里选一次打印机即可（设置会记住）。

### 环境要求
- Windows 7 SP1 及以上，系统带 .NET Framework 4.x（Win10/11 自带）；
- 需要先装好打印机驱动并添加打印机（共享打印机写 `\\服务器名\打印机名`）。

### 这一版修了什么
- 「合并 + 裁掉白边」时发票最右边的细边框线和彩色边条被裁掉（1.4.1 主要修复）；
- 详见 [CHANGELOG.md](https://github.com/MengzhongYiyu/PDFBatchPrint/blob/main/CHANGELOG.md)。

### 校验
'@

Write-Host '============================================' -ForegroundColor Cyan
Write-Host '   上传便携包到 GitHub Releases' -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan
Write-Host ''

if (-not (Test-Path $zipLocal)) {
  Write-Host ('[!] 找不到便携包: ' + $zipLocal) -ForegroundColor Red
  Write-Host '    先跑 make_portable.cmd 生成。'
  Read-Host '按回车退出'; exit 1
}
$mb = [math]::Round((Get-Item $zipLocal).Length / 1MB, 1)
Write-Host ('便携包: ' + (Split-Path $zipLocal -Leaf) + '  ' + $mb + ' MB')
$sha = (Get-FileHash $zipLocal -Algorithm SHA256).Hash.ToLower()
Write-Host ('SHA256: ' + $sha)
$body = $body + "`n- SHA256: " + $sha
Write-Host ''

Write-Host '[1/3] 输入访问令牌（和推送用的是同一个）' -ForegroundColor Yellow
if ([Console]::IsInputRedirected) {
  $token = (Read-Host '  粘贴访问令牌 PAT').Trim()
} else {
  $sec = Read-Host '  粘贴访问令牌 PAT（需要 repo 权限）' -AsSecureString
  $token = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
}
if (-not $token) { Write-Host '[!] 令牌为空' -ForegroundColor Red; Read-Host '按回车退出'; exit 1 }
Write-Host ''

$headers = @{ Authorization = 'token ' + $token; Accept = 'application/vnd.github+json'; 'User-Agent' = 'pdfbatch-release' }
$apiBase = 'https://api.github.com/repos/' + $owner + '/' + $repo

Write-Host '[2/3] 创建 release ' -ForegroundColor Yellow -NoNewline
Write-Host $tag
$releaseId = $null
$payload = @{ tag_name = $tag; name = $title; body = $body; draft = $false; prerelease = $false } | ConvertTo-Json
try {
  $rel = Invoke-RestMethod -Method Post -Uri ($apiBase + '/releases') -Headers $headers -Body $payload -ContentType 'application/json'
  $releaseId = $rel.id
  Write-Host ('  已创建: ' + $rel.html_url) -ForegroundColor Green
} catch {
  $msg = $_.Exception.Message
  Write-Host ('  创建失败: ' + $msg) -ForegroundColor DarkYellow
  if ($msg -match '422|already_exists') {
    try {
      $rel = Invoke-RestMethod -Method Get -Uri ($apiBase + '/releases/tags/' + $tag) -Headers $headers
      $releaseId = $rel.id
      Write-Host ('  该 tag 已存在，改为往现有 release 追加附件: ' + $rel.html_url) -ForegroundColor Yellow
    } catch { Write-Host ('  但也取不到现有 release: ' + $_.Exception.Message) -ForegroundColor Red }
  }
}
if (-not $releaseId) {
  Write-Host ''
  Write-Host '[!] 没能建 release。常见原因：' -ForegroundColor Red
  Write-Host '    1. 令牌没勾 repo 权限，或已过期'
  Write-Host '    2. 用户名/仓库名不对（应该是 MengzhongYiyu/PDFBatchPrint）'
  Write-Host '    3. 网络问题（api.github.com 需要能访问）'
  Read-Host '按回车退出'; exit 1
}

Write-Host ''
Write-Host ('[3/3] 上传附件 ' + $assetName + '（' + $mb + ' MB，请耐心等）...') -ForegroundColor Yellow
$uploadUrl = 'https://uploads.github.com/repos/' + $owner + '/' + $repo + '/releases/' + $releaseId + '/assets?name=' + $assetName


$r = & curl.exe -sS --retry 3 --retry-delay 3 --max-time 900 -X POST `
  -H ('Authorization: token ' + $token) `
  -H 'Content-Type: application/zip' `
  -H ('X-GitHub-Api-Version: 2022-11-28') `
  --data-binary ('@' + $zipLocal) `
  $uploadUrl 2>&1

Write-Host ''
if ($LASTEXITCODE -eq 0 -and $r -match 'browser_download_url') {
  $url = ([regex]::Match($r, '"browser_download_url":"([^"]+)"')).Groups[1].Value
  Write-Host '[OK] 上传成功！' -ForegroundColor Green
  Write-Host ('     下载地址: ' + $url) -ForegroundColor Green
  Write-Host ('     Release 页: https://github.com/' + $owner + '/' + $repo + '/releases/tag/' + $tag) -ForegroundColor Green
} else {
  Write-Host '[!] 上传失败' -ForegroundColor Red
  Write-Host ($r | Out-String).Trim()
  Write-Host '  也八成是网络：刷新一下 Release 页面看看附件有没有；没有就重跑本脚本。'
}
Write-Host ''
Read-Host '按回车关闭窗口'
