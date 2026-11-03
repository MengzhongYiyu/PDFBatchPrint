# -*- coding: utf-8 -*-
"""PDF批量打印工具 自检脚本（不需要真打印机）。

用法：  selftest.cmd            （推荐，自动找 python\\python.exe）
        python selftest.py      （需已装 pymupdf）
        python selftest.py --no-print   跳过「打印到 PDF」这一步

检查项对应 HANDOFF.md 第 9 节的「快速自检清单」：
  1) exe 能用 --help 输出
  2) Python + PyMuPDF 可用
  3) render_pdf.py info 报的页尺寸正确
  4) 合并 2up + 裁白边：4 张发票 -> 2 页 A4，且右边框没被裁掉（1.4.1 修复点）
  5) 打印到 Microsoft Print to PDF，页尺寸 = 420x297mm 横向
     （某些受限环境下会因权限被拒，此步只记 SKIP，不算失败）
"""
import os
import re
import shutil
import subprocess
import sys
import traceback

ROOT = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(ROOT, "_selftest")
IN = os.path.join(WORK, "in")
OUT = os.path.join(WORK, "out")
TMP = os.path.join(WORK, "tmp")
EXE = os.path.join(ROOT, "PdfBatchPrint.exe")
RENDER = os.path.join(ROOT, "render_pdf.py")
MM = 72.0 / 25.4

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

results = []


def record(name, status, detail=""):
    results.append((name, status, detail))
    line = "[%s] %s" % (status, name)
    if detail:
        line += "  -- " + detail
    print(line, flush=True)


def find_python():
    cand = [os.path.join(ROOT, "python", "python.exe")]
    env = os.environ.get("PDFBATCH_PYTHON")
    if env:
        cand.insert(0, env)
    if getattr(sys, "frozen", False) is False and sys.executable:
        cand.append(sys.executable)
    for p in cand:
        if p and os.path.isfile(p):
            try:
                r = subprocess.run([p, "-c", "import pymupdf"],
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   timeout=60)
                if r.returncode == 0:
                    return p
            except Exception:
                pass
    return None


def run(cmd, timeout=300):
    """跑外部命令，返回 (returncode, 合并输出)。"""
    env = dict(os.environ)
    env["TEMP"] = TMP
    env["TMP"] = TMP
    p = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                       timeout=timeout, env=env, cwd=ROOT)
    raw = p.stdout
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        text = raw.decode("gbk", "replace")
    return p.returncode, text


def make_fixtures(fitz):
    os.makedirs(IN, exist_ok=True)
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(TMP, exist_ok=True)

    def page(name, wmm, hmm, label):
        doc = fitz.open()
        pg = doc.new_page(width=wmm * MM, height=hmm * MM)
        r = pg.rect
        m = 10 * MM
        pg.draw_rect(fitz.Rect(m, m, r.width - m, r.height - m), color=(0, 0, 0), width=1.2)
        pg.insert_text((m + 8, m + 12), label, fontsize=14)
        path = os.path.join(IN, name)
        doc.save(path)
        doc.close()
        return path

    def invoice(name, wmm=210, hmm=140):
        doc = fitz.open()
        pg = doc.new_page(width=wmm * MM, height=hmm * MM)
        left, top = 12 * MM, 10 * MM
        right, bottom = (wmm - 12) * MM, (hmm - 10) * MM
        pg.draw_rect(fitz.Rect(left, top, right, bottom), color=(0.1, 0.1, 0.1), width=0.8)
        # 右侧彩色边条 + 贴边的竖线：1.4.1 修的正是这种零宽 drawing 被忽略的问题
        pg.draw_rect(fitz.Rect(right - 4 * MM, top, right - 0.2, bottom),
                     color=(0.8, 0.2, 0.2), fill=(0.95, 0.85, 0.85))
        pg.insert_text((left + 6, top + 14), name + " INVOICE", fontsize=11)
        path = os.path.join(IN, name)
        doc.save(path)
        doc.close()
        return path

    return {
        "a3": page("a3_land.pdf", 420, 297, "A3 LANDSCAPE"),
        "a2": page("a2.pdf", 594, 420, "A2 LANDSCAPE"),
        "a4": page("a4_port.pdf", 210, 297, "A4 PORTRAIT"),
        "invoices": [invoice("inv%d.pdf" % i) for i in range(1, 5)],
    }


def page_sizes(fitz, path):
    doc = fitz.open(path)
    try:
        return [(round(p.rect.width * 25.4 / 72, 1), round(p.rect.height * 25.4 / 72, 1))
                for p in doc]
    finally:
        doc.close()


def main():
    if not os.path.isfile(EXE):
        record("exe 存在", "FAIL", "找不到 " + EXE + "，先跑 build.cmd")
        return 1
    if not os.path.isfile(RENDER):
        record("render_pdf.py 存在", "FAIL", "找不到 " + RENDER)
        return 1

    do_print = "--no-print" not in sys.argv

    try:
        import pymupdf as fitz
    except ImportError:
        record("PyMuPDF 可导入", "FAIL", "pip install pymupdf，或用 python\\python.exe")
        return 1
    record("PyMuPDF 可导入", "PASS", "版本 " + str(getattr(fitz, "__version__", "?")))

    py = find_python()
    if not py:
        record("Python 运行时", "FAIL", "找不到可用 pymupdf 的 python（项目里的 python\\python.exe？）")
        return 1
    record("Python 运行时", "PASS", py)

    # 1) --help
    code, out = run([EXE, "--help"])
    if code == 0 and "--cli" in out:
        record("exe --help", "PASS")
    else:
        record("exe --help", "FAIL", "退出码 %d" % code)

    fixture = make_fixtures(fitz)

    # 2) render_pdf.py info
    code, out = run([py, RENDER, "info", fixture["a3"]])
    ok = code == 0 and re.search(r"P\s+420(\.0)?\s+297(\.0)?", out)
    record("render_pdf info（A3 420x297）", "PASS" if ok else "FAIL",
           (out.strip().splitlines() or [""])[-1][:80])

    # 3) 合并 2up + 裁白边
    merged = os.path.join(OUT, "merged_2up.pdf")
    if os.path.exists(merged):
        os.remove(merged)
    files = ";".join(fixture["invoices"])
    code, out = run([EXE, "--cli", "--files", files, "--merge", "2up", "--crop-invoice", "1",
                     "--paper", "A4", "--save-merged", merged, "--printer", "Microsoft Print to PDF",
                     "--print-to-file", os.path.join(TMP, "sink.pdf")])
    detail = ""
    ok = os.path.isfile(merged)
    if ok:
        sizes = page_sizes(fitz, merged)
        ok = len(sizes) == 2 and all(abs(w - 210.0) < 1.5 and abs(h - 297.0) < 1.5 for w, h in sizes)
        detail = "页尺寸 " + str(sizes)
        if ok:
            doc = fitz.open(merged)
            max_x1 = max([d["rect"].x1 for d in doc[0].get_drawings()] or [0])
            page_w = doc[0].rect.width
            doc.close()
            # 右边框应落在页面内、又贴近右边界（没被裁掉）
            ok_crop = (page_w * 0.9) <= max_x1 <= (page_w - 1)
            ok = ok and ok_crop
            detail += "，最右内容 x1=%.1f / 页宽 %.1f" % (max_x1, page_w)
            if not ok_crop:
                detail += "（右边框疑似被裁掉）"
    record("合并 2up + 裁白边（4 张 -> 2 页 A4）", "PASS" if ok else "FAIL", detail)

    # 4) 打印到 PDF（受限环境可能被拒，只记 SKIP）
    if do_print:
        pdf = os.path.join(OUT, "print_a3.pdf")
        if os.path.exists(pdf):
            os.remove(pdf)
        code, out = run([EXE, "--cli", "--files", fixture["a3"], "--printer",
                         "Microsoft Print to PDF", "--print-to-file", pdf,
                         "--paper", "auto", "--orient", "auto"])
        tail = [l for l in out.strip().splitlines() if l.strip()][-1:] or [""]
        if code == 0 and os.path.isfile(pdf):
            sizes = page_sizes(fitz, pdf)
            ok = len(sizes) == 1 and abs(sizes[0][0] - 420) < 2 and abs(sizes[0][1] - 297) < 2
            record("打印到 PDF（A3 横向）", "PASS" if ok else "FAIL", str(sizes))
        elif "拒绝访问" in out or "Permission denied" in out:
            record("打印到 PDF（A3 横向）", "SKIP",
                   "当前进程没有打印机权限（受限环境），请在桌面上双击 exe 验证")
        else:
            record("打印到 PDF（A3 横向）", "FAIL", "退出码 %d %s" % (code, tail[0][:80]))

    passed = sum(1 for _, s, _ in results if s == "PASS")
    failed = sum(1 for _, s, _ in results if s == "FAIL")
    skipped = sum(1 for _, s, _ in results if s == "SKIP")
    print("")
    print("结果: %d 通过, %d 失败, %d 跳过" % (passed, failed, skipped))
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception:
        traceback.print_exc()
        sys.exit(2)
