# PDF 批量打印工具 — 项目交接说明（HANDOFF）

版本：**1.4.1**（2026-11-03 归档）
归档人：上一台机器（上一台电脑）的会话负责人
用途：把这套工具与开发状态完整搬到另一台电脑继续维护

---

## 1. 这是什么

一个 Windows 桌面小工具（单文件 WinForms exe + 一个 Python 渲染脚本），用来**批量打印 PDF 和图片**，专门针对工程图纸（A2/A3/A4 混排）和电子发票（批量合并、裁白边、2 合 1 打印）。

主要能力：

| 能力 | 说明 |
| --- | --- |
| 批量文件 | PDF + png/jpg/jpeg/bmp/gif/tif/tiff/webp，可混在一起排队 |
| 打印引擎 | **精确渲染**（PyMuPDF 渲染成图再交给打印机，所见即所得，推荐）/ **WPS 原样打印**（调系统 WPS 打印 verb，矢量输出但依赖 WPS） |
| 纸张 | 自动（按原图）/ A4 / A3 / A5 / Letter |
| 方向 | 自动（按图纸宽高判断横竖）/ 横向 / 纵向 |
| 边距 | 0 / 1 / 2 / 3 / 5 mm 或任意 0–30mm，默认 1mm |
| 缩放 | 适合纸张 / 实际大小 |
| 分辨率 | 150 / 200 / 300 / 400 / 600 dpi（渲染引擎专用） |
| 其它 | 份数、单双面、灰度打印 |
| 合并 | 不合并 / 每页 1 张 / 每页 2 张 / 每页 4 张（发票场景）+ 裁掉白边 + 「合并另存…」 |
| 打印预览 | 主界面「开始打印」旁边；画纸面、蓝虚线=打印机可打印区、红块=会被裁掉的部分，滚轮/按钮缩放翻页 |
| 打印机选择 | 自动识别「占位端口 PORTPROMPT:」和「死端口 FILE:/COM/LPT」的打印机、标红提示并自动改选同名的网络共享机 |
| 设置持久化 | %APPDATA%\PdfBatchPrint\settings.ini（关闭窗口时保存） |
| CLI | 全部参数都有命令行入口，可脚本化批量跑 |

---

## 2. 目录结构

```
pdfbatch/
├─ PdfBatchPrint.exe        # 编译产物（WinForms，.NET Framework 4.8，C# 5）
├─ src/PdfBatchPrint.cs     # 全部 C# 源码，单文件，约 10 万字符
├─ render_pdf.py            # Python 渲染/合并脚本（PyMuPDF + Pillow），exe 调它
├─ build.cmd                # 用系统 csc.exe 编译 exe
├─ make_portable.cmd        # 打包「便携版」（内嵌 Python + PyMuPDF + Pillow）
├─ selftest.cmd / selftest.py # 一键自检（编译产物 / 渲染 / 合并裁白边 / 打印到 PDF）
├─ README.md                # 功能说明 / CLI 参考 / FAQ（给使用者看）
├─ 使用说明.txt             # 给最终用户看的简短版（CRLF）
├─ HANDOFF.md               # 本文件（给开发者看）
├─ python/                  # 开发用 Python 3.12 + PyMuPDF + Pillow（106MB，.gitignore 忽略）
│                           # exe 自动使用「同目录 python\python.exe」，本机无需再配环境
├─ dist/
│  ├─ PDF批量打印工具/      # make_portable.cmd 生成的展开目录（可删，随时重生成）
│  └─ PDF批量打印工具_便携版_v1.4.1.zip   # 便携版压缩包（41MB，目标机免装 Python）
├─ _t/                      # 开发期一次性脚本和截图（探针、量尺寸、复现脚本），可删
├─ temp/                    # 运行时临时目录（空）
└─ __pycache__/             # Python 缓存，可删
```

**归档里没有放 `dist/PDF批量打印工具/`（解包后的 107MB 运行目录）**，因为同一个东西已经压在 `dist/PDF批量打印工具_便携版_v1.4.1.zip` 里了，需要时解压即可。

---

## 3. 依赖与运行环境

- Windows 10/11，需要 **.NET Framework 4.8**（Win10 1903+ 自带）
- **Python 3.12**（实测 3.12.14），装两个包：
  ```
  pip install pymupdf pillow
  ```
  - PyMuPDF 实测 2.0（旧的 1.2x 也能跑，但 `page.get_drawings()` 的字段要留意，见第 7 节）
  - Pillow 只在遇到 webp/部分 tif 等 PyMuPDF 打不开的图片时才用
- **WPS Office**（可选）：只有「WPS 原样打印」引擎和系统里 PDF 双击打开才需要；不装也能用「精确渲染」

**Python 查找顺序**（`PdfEngine.ResolvePython`）：
1. 命令行 `--python`
2. settings.ini 里的 `PythonPath=`
3. 环境变量 `PDFBATCH_PYTHON`
4. exe 同目录 `python\python.exe`（便携版就是这个）
5. `C:\Python312\python.exe`、PATH 里的 python

找到后会跑一次 `import pymupdf`（兼容 `import fitz`）自检，失败就继续找下一个。

---

## 4. 编译

```cmd
cd /d <项目目录>
build.cmd
```

等价于（csc 只能用 C# 5 语法，别写 C# 6+ 的字符串插值/表达式成员）：

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -codepage:65001 -optimize+
  -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Core.dll -r:System.Management.dll
  -out:PdfBatchPrint.exe src\PdfBatchPrint.cs
```

注意：
- **编译前先关掉正在运行的 PdfBatchPrint.exe**，否则报 `CS0016: 未能写入输出文件`。
- `-r:System.Management.dll` 不能少（枚举打印机端口用 Win32_Printer）。
- 在 PowerShell 里直接跑要写成 `& 'C:\...\csc.exe' -nologo ...`；在 Git Bash/MSYS 里跑会被路径转换搞坏参数，别用。

## 5. 打包便携版

```cmd
make_portable.cmd
```
会：复制 exe + render_pdf.py + 文档 → 从本机 DSH 运行时拷一份 Python（含 pymupdf、Pillow）→ robocopy 到 `dist\PDF批量打印工具\` → 自检 `import pymupdf / PIL` → 压成 `dist\PDF批量打印工具_便携版_v1.4.1.zip`。脚本里写死了源 Python 路径（`C:\Users\<用户名>\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python`），**换机器后要改这一行**。

---

## 6. 关键代码地图

### PDF 批量打印工具/src/PdfBatchPrint.cs
- `Program.Main` — `--cli` 分派到 Cli，否则开 GUI；`--help` 打印用法
- `Settings` / `AppPaths` — settings.ini 读写（`%APPDATA%\PdfBatchPrint\settings.ini`）
- `PdfEngine` — 找 Python（`ResolvePython`）、调脚本：`GetInfo` / `Render` / `Merge` / `Img2Pdf`；`CollectFiles`、`IsSupportedPath`、`SupportedFilter` 决定收哪些文件
- `BatchPrinter` — 主打印循环 `Run`
  - `PrintViaRender` → 渲染 PNG → `PrintImages`
  - `PrintImages` → `PrintDocument` + `StandardPrintController`，`QueryPageSettings` 里定纸张/方向，`PrintableArea` 算可打印区（**原点就是可打印区左上角，不要再用 MarginBounds.X/Y 偏移**），`FitRect` 等比摆放
  - `ResolvePaper` / `ResolveLandscape` / `PaperLabel` — 自动纸张与横竖
  - `EffectiveDpi` — 按目标纸张限制成图总像素（约 45M 上限），防止 A3@600dpi 渲染爆内存
  - `MergeActive` / `NupValue` — 合并模式
  - `PrintViaWps` — ShellExecute `Verb=print`
  - `TempRoot` — 临时目录探测（%TEMP% → %LOCALAPPDATA% → exe 目录）
- `Cli` — 全部 `--` 参数解析 + `Usage`
- `MainForm` — 界面（TableLayoutPanel 布局）；`LoadPrinters`+`PickPrinter`+`DeadPort`+`PreferShare` 是打印机挑选逻辑；`StartPrint`；`PreviewClick`/`StartPreview`/`BuildPreviewPages`+`PreviewForm` 是预览

### render_pdf.py
- `cmd_info` — 输出 `F<TAB>路径<TAB>页数` 和 `P<TAB>宽mm<TAB>高mm`
- `cmd_render` — 逐页渲染 PNG，输出 `P<TAB>页号<TAB>png路径`
- `cmd_merge` — 合并排版（`show_pdf_page` 矢量化），支持 `nup` 1/2/4、`crop`（裁白边）、纸张 A4/A3；`PAD_MM=2.0` 是格子内边距
- `cmd_img2pdf` — 图片转 PDF（给 WPS 引擎用，也供合并）
- `open_doc` — PDF 直开；图片走 `convert_to_pdf`，失败回落 Pillow
- `content_box` + `_union` + `CROP_PAD` — **裁白边的核心，见第 7 节第 1 条**
- `cell_rects` / `fit_rect` — 合并时每个格子的位置与等比缩放

---

## 7. 踩过的坑（务必先读，能省几个小时）

1. **裁白边不能直接用 PyMuPDF 的 `Rect | Rect` 求并集。**
   `Rect.__or__` 会**忽略零宽/零高的矩形**，而 PDF 里的竖边框线常常就是 `x0==x1` 的 drawing。用 `|` 算出来的内容框会漏掉边框，`clip=` 一切就把发票右边框和彩色边条裁掉。
   正解：`_union()` 手工按 min/max 合并，drawings 再按 `width/2` 外扩，最后整体外扩 `CROP_PAD=1.0pt`。（这是 1.4.1 修的 bug）

2. **GDI+ 打印坐标：PrintPage 的 Graphics 原点就是可打印区左上角**（`OriginAtMargins=false` 时），`MarginBounds` 的 X/Y 是硬边距偏移，**不能拿 `MarginBounds.X/Y` 当绘制原点**，否则整体右下偏移一个硬边距，A3→A4 时底部被裁 3mm。见 `PrintableArea()`。

3. **`PrintDocument` 打印到「Microsoft Print to PDF / XPS」时，如果 exe 运行在某些受限环境下会抛 `Win32Exception: 拒绝访问`（errno=5）**，但同一份代码在别处正常、原生 winspool API 也正常 —— 这是执行环境限制，不是程序 bug。测试虚拟打印机时请把 exe 复制到 `C:\Windows\Temp\xxx` 之类的工作目录外再跑。

4. **WPS 引擎打印图片**：WPS 不能直接「打印」图片文件当 PDF 用，所以先 `img2pdf` 转临时 PDF 再 ShellExecute，日志会写「图片已转为临时 PDF」。

5. **图片的物理尺寸靠 dpi 推算**：截图/手机照片常是 72/96dpi，会被算成 A2/A3 大小。遇到「照片打印成 A3」就把纸张手动选 A4。

6. **合并的一次性渲染**：早期版本在 `foreach (page)` 里每页都 `Render` 整个 PDF，导致 N×N 重复页。现在只渲染一次再逐页打印。

7. **PowerShell 5.1 用 `-File` 跑含中文的 .ps1 必须存成 UTF-8 带 BOM**，否则按 ANSI 读，字符串比较永远不匹配。

8. **命令行传参**：`Start-Process -ArgumentList @(...)` 不会给带空格的值加引号，必须把整个参数串拼成一个字符串传进去。

---

## 8. 已知问题 / 待办

| # | 问题 | 状态 / 建议 |
| --- | --- | --- |
| 1 | A2 图纸（594×420mm）在打印机最大 A3 时会缩到 A3 打印 | 有意为之（README 已写）；如要保真需支持 A2 纸或分块打印 |
| 2 | 图片按自带 dpi 换算物理尺寸，低 dpi 照片会判成 A3 | 已在 README FAQ 提示手动选 A4；可考虑加「图片按纸张铺满」选项 |
| 3 | WPS 引擎打印图片依赖 Python | 已在代码里给出明确报错；可改用渲染引擎 |
| 4 | 现场那台 `\\打印服务器\HP MFP M433 PCL 6` 队列曾卡死（job 18 起一直 Printing/Retained），非本工具问题 | 需要 打印服务器 管理员：`Remove-PrintJob` 或 `Stop-Service Spooler`；`_t\fix_print_queue.ps1` + `_t\修复打印机所在电脑.cmd` 就是给 打印服务器 管理员用的 |
| 5 | 本机曾出现打印机连接丢失、默认打印机被改成 `HP Universal Printing PS`（端口 FILE:），导致所有程序「打印没反应」 | 修复：`Add-Printer -ConnectionName '\\打印服务器\HP MFP M433 PCL 6'` 再 `SetDefaultPrinter`。工具现在也会检测「死端口」并跳过 |
| 6 | 预览只渲染前 80 页 | 够用，如要全量可改 `MaxPreviewPages` 常量 |

---

## 9. 换到新电脑怎么接着做

1. 解压归档到例如 `D:\work\`，目录形如 `PDF批量打印工具_项目归档\pdfbatch\`。
2. **先跑通成品**：双击 `pdfbatch\PdfBatchPrint.exe`，或解压 `dist\PDF批量打印工具_便携版_v1.4.1.zip` 后运行里面的 exe（免装 Python）。
3. **验证渲染链路**：
   ```cmd
   PdfBatchPrint.exe --help
   python render_pdf.py info 某个.pdf
   ```
   第一条打印用法，第二条输出 `P<TAB>420<TAB>297` 之类。
4. **验证打印链路**（不会真出纸）：打印机选 `Microsoft Print to PDF`，勾 `--print-to-file` 跑一遍 CLI，看是否 exit 0 且生成 PDF、页尺寸对不对。
5. **要改代码**：改 `src/PdfBatchPrint.cs` / `render_pdf.py` → `build.cmd` → 复制 exe+py 到便携版目录 → `make_portable.cmd`（先改里写死的 Python 源路径）。
6. **要改行为前先看第 7 节**，这几个坑位置很隐蔽。

### 快速自检清单（改完务必跑）
- [ ] `PdfBatchPrint.exe --help` 正常输出
- [ ] 单文件 A3 图纸 → `--paper auto --orient auto` → 输出 PDF 页尺寸 420×297mm 横向
- [ ] A3 图纸 → `--paper A4` → 内容完整不裁边（看 PDF 四角）
- [ ] 发票 4 张 → `--merge 2up --crop-invoice 1` → 每页 2 张、四边边框齐全
- [ ] GUI 能启动、打印机列表正确、预览窗口能开
- [ ] 便携版里 `python\python.exe -c "import pymupdf, PIL"` 通过

---

## 10. settings.ini 参考

位置：`%APPDATA%\PdfBatchPrint\settings.ini`（UTF-8 无 BOM）

```ini
# PdfBatchPrint 设置
Printer=\\打印服务器\HP MFP M433 PCL 6
Engine=render
Copies=1
Duplex=simplex
Paper=auto
Orientation=auto
Merge=2up
CropInvoice=1
Scale=fit
Dpi=300
MarginMm=1
Gray=0
PythonPath=
FormWidth=1220
FormHeight=800
```

换机器后 `Printer=` 要改成新环境的打印机名（GUI 里选一次也会自动写回）。

---

## 11. 本机环境状态（2026-11-03 导入这台电脑时已就绪）

> 这一段是「换机后」的落地记录，上面 1–10 节仍然是通用说明。

| 项 | 本机实际 |
| --- | --- |
| 项目路径 | `C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch` |
| 系统 | Windows 10 专业版 19045（.NET Framework 4.8，Release 533325） |
| 编译器 | `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，`build.cmd` 已验证可直接编译（exe 71168 字节） |
| Python | 项目内 `python\`（3.12.14 + PyMuPDF 2.0 + Pillow 12.3.0，从便携包解出）→ `ResolvePython` 第 4 条候选直接命中，**不用设 PDFBATCH_PYTHON** |
| 打印机 | 本机**没有物理打印机**，只有 `Microsoft Print to PDF` / XPS / OneNote / Fax；默认打印机 = `Microsoft Print to PDF` |
| WPS | **未安装** → 「WPS 原样打印」引擎在本机不可用；日常验证走「精确渲染 + Microsoft Print to PDF」 |
| 便携包 | 已用改进后的 `make_portable.cmd` 重新生成（41.1MB），解压自测通过（含 `使用说明.txt`） |
| 版本控制 | 已 `git init`（分支 `main`），基线提交 = v1.4.1 归档；`python/`、`dist/`、`PdfBatchPrint.exe`、`temp/`、`_selftest/` 已忽略（均是可重生成的产物） |

### 本机已做的改动（相对归档原件）

1. `build.cmd`：去掉 UTF-8 BOM（否则 cmd 首行报「`﻿@echo` 不是内部或外部命令」）；加「exe 正在运行就别编译」的前置检查（对应 CS0016）；`find` 改用 `%WINDIR%\System32\find.exe` 绝对路径（PATH 被 MSYS 工具污染时会找错）；全文保持 ASCII，换机器不乱码。
2. `make_portable.cmd`：Python 源路径不再写死 `C:\Users\<用户名>\...`，改为「优先 `%~dp0python`，其次 `%USERPROFILE%\.dsh\dsh-runtimes\*\dependencies\python`」，找不到才报错；补 `chcp 65001` 保证中文文件名（`使用说明.txt`）能复制；加 `setlocal`。
3. 新增 `python\`（开发运行时，已 gitignore）、`selftest.cmd` / `selftest.py`、`.gitignore`、`.gitattributes`。
   注意 `PdfBatchPrint.exe` 作为编译产物已 **不纳入 git**（`build.cmd` 两秒即可重建），所以 `git status` 在编译后依然干净。

### 日常命令

```cmd
cd /d "C:\mengzhongyiyu\laji3\PDF批量打印工具_项目归档_v1.4.1_20261103\pdfbatch"

build.cmd        :: 改完 src\PdfBatchPrint.cs 后编译（先关掉正在运行的 exe）
selftest.cmd     :: 6 项自检，改完代码跑一遍，退出码 0 = 全过
make_portable.cmd:: 重新打便携包（打包 106MB + 压缩，约 25 秒）

PdfBatchPrint.exe                          :: 直接跑开发版（用同目录 python\）
dist\PDF批量打印工具\PdfBatchPrint.exe   :: 跑便携版（免装 Python，双击即可）
```

`selftest.cmd` 检查：PyMuPDF / exe --help / render_pdf.py info（A3 420×297）/ 合并 2up+裁白边（4 张→2 页 A4 且右边框没被裁）/ 打印到 PDF（A3→420×297 横向）。

### 本机特有的两个坑（HANDOFF 第 7 节之外新增）

1. **从脚本/自动化环境（非交互桌面）调用 exe 打印**时，`PrintDocument.Print()` 可能抛 `Win32Exception (0x80004005): 拒绝访问`——这是执行环境的权限限制（第 7 节第 3 条的同一现象），不是程序 bug；在桌面上双击 exe 正常。自检脚本遇到这种情况只记 `SKIP`，不判失败。
2. **同一环境里 `%LOCALAPPDATA%\Temp` 可能不可写**，渲染 PNG 会报 `cannot open file ...\PdfBatchPrint\...\p0001.png: Permission denied`。跑 CLI/自检时把 `TEMP`/`TMP` 指到工作区目录即可（`selftest.py` 已自动这么处理）。

### 下一步可以做的（第 8 节待办之外）

- 便携包文件名已统一为 `PDF批量打印工具_便携版_v1.4.1.zip`（原来叫 `_v1.4.zip`，与实际版本不一致），`使用说明.txt` / README 里的引用已同步。
- `_t\` 是上一台机器的开发期脚本，留着当参考；确认没用可以删。
- 本机没有 WPS，若要在本机回归「WPS 原样打印」引擎，需要先装 WPS Office。
