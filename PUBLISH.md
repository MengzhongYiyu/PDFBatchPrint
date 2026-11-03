# 把项目发布到 GitHub（手把手）

> 目标：让这个项目出现在 <https://github.com/MengzhongYiyu/PDFBatchPrint>
> 说明：这台电脑**还没有安装 Git**，所以下面走图形工具 **GitHub Desktop**（自带 Git，不用背命令）。
> 第一次大约 10 分钟，之后每次更新只要「Commit → Push」两下。

---

## 0. 现在的状态（已经准备好的部分）

| 项 | 状态 |
| --- | --- |
| Git 仓库 | ✅ 已在 `pdfbatch\` 目录里初始化，分支 `main`，有一个基线提交 |
| 提交内容 | ✅ 只提交源码和文档（**371 KB**，30 来个文件） |
| 不入库的东西 | ✅ `python\`（107MB 运行时）、`dist\`（41MB 便携包）、`PdfBatchPrint.exe`、`temp\`、`_selftest\`（见 `.gitignore`） |
| 文档 | ✅ `README.md`（含截图）、`HANDOFF.md`、`CHANGELOG.md`、`LICENSE`（MIT）、`PUBLISH.md` |
| 敏感信息 | ✅ 内部主机名已改成「打印服务器」占位，旧机器名/管理员路径已清掉 |

> 为什么不能直接把 107MB 的 `python\` 也传上去？GitHub 对单仓库有大小限制，而且运行时会随版本变得很大。
> 便携包用 `make_portable.cmd` 随时能重新打出来，所以放 `dist\`、不入库。

---

## 1. 装 GitHub Desktop

**方法 A（最快，我来装）**：我在这台电脑上执行
```cmd
winget install --id GitHub.GitHubDesktop -e --accept-source-agreements --accept-package-agreements
```
装完在开始菜单里找 **GitHub Desktop**。

**方法 B（自己下）**：打开 <https://desktop.github.com/> → Download for Windows → 双击安装。

首次打开会让你登录：
1. 没有账号先注册：<https://github.com/signup>（要邮箱，记得验证）
2. 已有账号点 **Sign in to GitHub.com** → 浏览器里点绿色 **Authorize desktop** → 回到窗口
3. 如果弹出 "Configure Git"，Name 填名字、Email 填登录 GitHub 用的邮箱（随意，但邮箱建议和账号一致）

---

## 2. 把本地项目交给 GitHub Desktop

菜单 **File → Add local repository…**

- Local path 选这个目录（注意是 `pdfbatch`，不是外面那层长名字的归档目录）：

```
C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch
```

- 点 **Add repository**。
- 如果提示 *"This directory does not appear to be a Git repository"*，说明少选/多选了一层目录。

加进来后左侧会出现仓库名（`pdfbatch`），历史里能看到提交 **v1.4.1 归档基线**。

---

## 3. 发布（Publish repository）

窗口右上角有一个 **Publish repository** 按钮，点它：

| 字段 | 填什么 |
| --- | --- |
| Name | `PDFBatchPrint` |
| Description | `Windows 批量打印 PDF/图片的小工具：A2/A3 图纸自动纸张与横竖、发票合并裁白边、逐页预览` |
| Keep this code private | **改成不勾选**（你要公开）；想先私有可以勾着，以后在网页 Settings 里改 |

点 **Publish repository**，等 10~30 秒。

---

## 4. 检查发布结果

- 窗口里点 **Repository → View on GitHub**，或直接打开
  <https://github.com/MengzhongYiyu/PDFBatchPrint>
- 应该看到：README 正常渲染（含截图）、绿色 MIT 许可证标识、`src/` 里有源码。
- 仓库大小应该只有几百 KB —— 这是对的。

---

## 5. 以后每次改代码怎么更新

### 用 GitHub Desktop（推荐）

1. 左侧 **Changes** 会列出你改过的文件
2. 左下角 Summary 写一句这次改了什么（例如 `修复 A3 缩 A4 底部被裁`）
3. 点 **Commit to main**
4. 顶部出现 **Push origin** 按钮 → 点它

### 或者命令行（装了 Git 之后）

```cmd
cd /d "C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch"
git status                 :: 看改了哪些文件
git add -A                 :: 全部加入这次提交
git commit -m "说明这次改了什么"
git push                   :: 推到 GitHub
```

---

## 6. 常见问题

**Q: 点 Publish 报 `name already exists`？**
说明你账号里已经有同名仓库。换个名字（例如 `PDFBatchPrint-tool`），或先把旧的那个删掉/改名。

**Q: 卡在 Publishing / 上传失败？**
国内网络访问 GitHub 偶尔不稳。重试一次；仍然不行就挂代理（GitHub Desktop 设置里没有代理项，需要系统代理或 `git config --global http.proxy http://127.0.0.1:端口`）。

**Q: 提示 `Authentication failed`？**
退出重新登录：GitHub Desktop → File → Options → Accounts → Sign out → Sign in。

**Q: 我不小心把某个文件传上去了，想删掉？**
图省事：在网页上删文件 + 提交；彻底清理历史需要 `git filter-repo`，可以叫我做。

**Q: 想把 `_t\` 这些开发期脚本也去掉？**
`git rm -r --cached _t` + 在 `.gitignore` 里加 `/_t/`，然后提交即可（本地文件不受影响）。

**Q: 想让仓库首页更好看？**
README 里已经有截图和目录；也可以加徽章（badge），但本项目没有 CI，加了也只是装饰。

---

## 7. 附：如果你更喜欢命令行（可选）

不想裝 GitHub Desktop 的话，装 **Git for Windows**（<https://git-scm.com/download/win>），然后：

1. **配置身份**（只做一次）
   ```cmd
   git config --global user.name "MengzhongYiyu"
   git config --global user.email "你的GitHub邮箱"
   ```
2. **在网页上建空仓库**：<https://github.com/new> → 名字 `PDFBatchPrint` → **不要**勾 README / .gitignore / license
3. **生成访问令牌（PAT）**：头像 → Settings → Developer settings → Personal access tokens →
   **Tokens (classic)** → Generate new token (classic) → 勾 `repo` → 生成后立刻复制（只显示一次）
4. **关联并推送**
   ```cmd
   cd /d "C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch"
   git remote add origin https://github.com/MengzhongYiyu/PDFBatchPrint.git
   git branch -M main
   git push -u origin main
   ```
   弹窗要账号密码时：用户名 = GitHub 用户名，密码 = **粘贴 PAT**（不是登录密码）。
5. 以后 `git add -A` / `git commit -m "..."` / `git push`。

> 也可以用 GitHub CLI：`winget install --id GitHub.cli`，然后
> `gh auth login`（浏览器授权）→ `gh repo create PDFBatchPrint --public --source=. --push`。
> 注意本机 `api.github.com` 未认证访问会被限流（403），登录后就正常了。

---

## 8. 发布前检查清单

- [x] `git status` 干净，分支是 `main`
- [x] `.gitignore` 排除了 `python\`、`dist\`、`PdfBatchPrint.exe`、`temp\`、`_selftest\`
- [x] README 有截图、构建说明、许可证链接
- [x] LICENSE / CHANGELOG / HANDOFF 齐全
- [x] 内部主机名、旧机器名、管理员路径已清理
- [ ] （你）GitHub 账号已登录 GitHub Desktop
- [ ] （你）点 **Publish repository** 时确认 **Keep this code private 未勾选**
- [ ] （你）发布后访问一次仓库首页，确认 README 和截图正常
