# 把项目发布到 GitHub（手把手）

> 目标：让这个项目出现在 <https://github.com/MengzhongYiyu/PDFBatchPrint>
> 本机现状：**没有安装 Git for Windows，也没有 GitHub Desktop**；但 DSH 自带了一个可用的 git，
> 所以下面这条路**什么都不用装**，4 步搞定。整个仓库只有 371 KB，推送很快。

---

## 0. 已经准备好的部分（我做完的）

| 项 | 状态 |
| --- | --- |
| Git 仓库 | ✅ 已初始化在 `pdfbatch\`，分支 `main`，3 个提交 |
| 远程地址 | ✅ 已设为 `https://github.com/MengzhongYiyu/PDFBatchPrint.git` |
| 上传内容 | ✅ 源码 + 文档，共 **32 个文件 / 371 KB** |
| 排除内容 | ✅ `python\`（107MB）、`dist\`（41MB）、`PdfBatchPrint.exe`、`temp\`、`_selftest\` |
| 文档 | ✅ `README.md`（含截图）、`HANDOFF.md`、`CHANGELOG.md`、`LICENSE`(MIT) |
| 内部信息 | ✅ 内部主机名已改为「打印服务器」，旧机器名/管理员路径已清理 |
| 一键推送 | ✅ `发布到GitHub.cmd`（双击运行）+ `publish-to-github.ps1` |

> **为什么先不用 GitHub Desktop？** 这台机器没有装 Git 和 GitHub Desktop；
> 实测直连 GitHub 下载只有约 **37 KB/s**，代理只把速度提到 0.3–0.45 MB/s，
> 而安装包有 **256MB**（下到 93MB 会卡住），所以图形工具留到网络稳定时再装（见第 7 节）。
> **推送本身只有约 400KB，直连就够**，不需要代理 —— 这就是下面这条路的价值。

---

## 1. 第一步：在网页上创建空仓库

1. 浏览器打开 <https://github.com/new>
2. 填：
   - **Repository name**：`PDFBatchPrint`（必须一模一样，大小写也要对）
   - **Description**（可选）：`Windows 批量打印 PDF/图片的小工具：A2/A3 图纸自动纸张与横竖、发票合并裁白边、逐页预览`
   - 选 **Public**
   - **下面三个勾全部不要勾**：Add a README file / Add .gitignore / Choose a license
     （勾了就会和本地仓库冲突，还得额外处理）
3. 点绿色 **Create repository**，出现的页面先别关。

---

## 2. 第二步：生成访问令牌（PAT，当密码用）

GitHub 从 2021 年起**不再接受账号密码推送**，必须用令牌。

1. 点右上角头像 → **Settings** → 左侧最下面 **Developer settings**
2. **Personal access tokens** → **Tokens (classic)** → 右上 **Generate new token (classic)**
3. 填：
   - **Note**：`pdfbatch-push`
   - **Expiration**：90 days（到期重新生成一个即可）
   - **Select scopes**：只勾最上面那个 **`repo`**（整块）
4. 点最下面 **Generate token** → 页面顶部出现一串 `ghp_xxxxxxxx`，
   **立刻复制**（只显示这一次，刷新就看不见了）→ 先粘到记事本里备用。

> 令牌 = 密码，别发给别人、别写进代码。用完可以随时在同一个页面撤销（Delete）。

---

## 3. 第三步：双击推送

在文件资源管理器里打开：

```
C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch
```

双击 **`发布到GitHub.cmd`**，会弹出一个黑色窗口：

1. 窗口里出现 `Username for 'https://github.com':` → 输入你的 GitHub 用户名，回车
2. 出现 `Password for 'https://...':'` → **粘贴刚才那串令牌**，回车
   （粘贴时屏幕上不会有任何字符，这是正常的，别以为没粘上）
3. 看到 `main -> main` 和 `[OK] 推送成功！` 就成了，按任意键关窗

脚本会自动：找到 git（系统装的或 DSH 自带的）→ 检查/添加远程 → 推送 `main`。

---

## 4. 第四步：验证

浏览器打开 <https://github.com/MengzhongYiyu/PDFBatchPrint>，应该看到：

- README 正常渲染，含主界面截图
- 文件列表里有 `src/`、`render_pdf.py`、`build.cmd`、`LICENSE`…
- 右侧 About 区显示 MIT License
- 仓库大小只有几百 KB（正常，`python\` 和便携包被 .gitignore 排除了）

---

## 5. 以后每次改代码怎么更新

### 用我们自己的脚本（还是双击）

脚本只做了「推送」，所以先用 git 提交，再双击。命令行方式（PowerShell 或 cmd）：

```cmd
cd /d "C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch"

:: 看改了哪些文件
git status

:: 全部加入本次提交
git add -A

:: 写一句这次改了什么
git commit -m "修复 A3 缩 A4 底部被裁"

:: 推送（或者双击 发布到GitHub.cmd）
git push
```

> 本机没有全局 git 身份，第一次提交前如果要写名字，可在项目里设置（只影响这个仓库）：
> `git config user.name "MengzhongYiyu"` 和 `git config user.email "你的邮箱"`

### 检查提交是否干净

```cmd
git status          :: 应该显示 nothing to commit
git log --oneline   :: 看提交历史
```

---

## 6. 常见问题

**Q: 双击后窗口一闪就没了？**
被安全策略拦了，用 PowerShell 手动跑：
`powershell -NoProfile -ExecutionPolicy Bypass -File "\发布到GitHub.cmd 所在目录\publish-to-github.ps1"`

**Q: 报 `could not read Username for 'https://github.com'`？**
说明 git 没拿到输入（或用了非交互终端）。直接双击 `发布到GitHub.cmd` 在真实窗口里跑，别在自动化环境里跑。

**Q: 报 `Repository not found` / 403？**
① 网页上还没建仓库，或名字拼错（要完全一致 `PDFBatchPrint`）；
② 令牌没勾 `repo` 权限，或复制时少了几位；
③ 用户名填错（是 GitHub 用户名，不是邮箱）。

**Q: 报 `rejected ... non-fast-forward`？**
建仓库时勾了 README。先 `git pull --rebase origin main` 再 `git push`；
或者干脆把网页上那个仓库删掉，重新建一个空的。

**Q: 每次推送都要输一次令牌，太麻烦？**
那是因为脚本里特意用了 `-c credential.helper=`（不落盘保存）。三种办法：
① 就这么输，安全；
② 装 **Git for Windows**（<https://git-scm.com/download/win>），它会带凭据管理器，输一次记住；
③ 装 **GitHub Desktop**，登录一次，以后点按钮。

**Q: 想把 `_t\` 这些开发期脚本去掉？**
`git rm -r --cached _t`，然后在 `.gitignore` 里加一行 `/_t/`，提交即可（本地文件不受影响）。

**Q: 以后想加个 CI / 自动发布？**
加 `.github/workflows/build.yml`，用 `windows-latest` 跑 `build.cmd`（需要 .NET Framework 4.8，runner 自带）。
要先有 GitHub 账号，再谈这个；需要的话叫我。

---

## 7. 备选：GitHub Desktop（网络好的时候）

如果以后网速正常了，装有图形界面的 GitHub Desktop 会更省心（自带 Git 和凭据、不用令牌）：

1. 下载安装：<https://desktop.github.com/>（或用 winget）
   ```cmd
   winget install --id GitHub.GitHubDesktop -e --accept-source-agreements --accept-package-agreements
   ```
2. 打开后 **Sign in to GitHub.com**（浏览器授权）
3. **File → Add local repository…** → 选 `pdfbatch` 目录
4. 右上 **Publish repository** → Name 填 `PDFBatchPrint` → **取消勾选 Keep this code private** → Publish
5. 以后改动：左侧 **Changes** → 写 Summary → **Commit to main** → **Push origin**

> 注意：如果用这条路线，就不要再用第 3 节的双击脚本（同一个仓库两种推法会打架，但不会坏数据）。

---

## 8. 附：纯命令行（不依赖脚本）

```cmd
cd /d "C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch"

:: 远程已经配好了，直接推
git remote -v
git push -u origin main

:: 如果远程没配过：
git remote add origin https://github.com/MengzhongYiyu/PDFBatchPrint.git
git push -u origin main
```

也可以用 GitHub CLI（需要先装）：`gh auth login` → `gh repo create PDFBatchPrint --public --source=. --push`。

---

## 9. 发布前检查清单

- [x] `git status` 干净，分支 `main`，远程已指向 `MengzhongYiyu/PDFBatchPrint`
- [x] `.gitignore` 排除了 `python\`、`dist\`、`PdfBatchPrint.exe`、`temp\`、`_selftest\`
- [x] README 有截图、构建说明、许可证链接
- [x] LICENSE / CHANGELOG / HANDOFF 齐全
- [x] 内部主机名、旧机器名、管理员路径已清理
- [ ] （你）网页上建好空仓库 `PDFBatchPrint`
- [ ] （你）生成 PAT 并勾选 `repo`
- [ ] （你）双击 `发布到GitHub.cmd` 并输入用户名 + 令牌
- [ ] （你）打开仓库首页确认 README 和截图正常
