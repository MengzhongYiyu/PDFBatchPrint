# -*- coding: utf-8 -*-
"""PDF 渲染辅助：为 PdfBatchPrint.exe 提供页数/尺寸信息、逐页位图渲染与发票合并。
用法:
  python render_pdf.py info <文件> [<文件> ...]
      输出: F<TAB>路径<TAB>页数  然后每页 P<TAB>宽mm<TAB>高mm
      文件可以是 PDF，也可以是图片（png/jpg/jpeg/bmp/gif/tif/tiff/webp），
      图片的"页尺寸"按图片自带的 dpi 换算成毫米（没有 dpi 信息时按 96dpi）。
  python render_pdf.py render <文件> <输出目录> <dpi> <gray:0/1>
      输出: P<TAB>序号<TAB>png路径
  python render_pdf.py merge <输出pdf> <nup:1|2|4> <crop:0|1> <paper:A4|A3> <列表文件>
      列表文件为 UTF-8，每行一个文件路径（PDF 或图片）；输出: M<TAB>输出pdf<TAB>页数
  python render_pdf.py img2pdf <输出pdf> <图片> [<图片> ...]
      把图片转成 PDF（每张一页，页尺寸=图片按 dpi 换算的实际尺寸）；输出: M<TAB>输出pdf<TAB>页数
"""
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
try:
    import pymupdf as fitz
except Exception:
    try:
        import fitz
    except Exception as exc:
        sys.stderr.write("NO_PYMUPDF: %s\n" % exc)
        sys.exit(3)

PAPERS = {
    "A4": (595.276, 841.89),
    "A3": (841.89, 1190.55),
}
PAD_MM = 2.0
CROP_PAD = 1.0  # 裁白边时向外留的余量(pt)，避免把细边框线/彩色边条裁掉

IMAGE_EXTS = (
    ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".bmp", ".gif",
    ".tif", ".tiff", ".webp", ".pnm", ".pgm", ".ppm", ".pcx", ".tga", ".psd", ".jxr",
)


def is_image(path):
    return os.path.splitext(path)[1].lower() in IMAGE_EXTS


def _image_to_pdf_bytes(path):
    """图片 -> PDF 字节。优先用 PyMuPDF 直接转换（保留 dpi），失败时用 Pillow 先转 PNG。"""
    raw = fitz.open(path)
    try:
        return raw.convert_to_pdf()
    finally:
        raw.close()


def open_doc(path):
    """打开 PDF 或图片，统一返回 PDF 文档（图片会先转成等尺寸的单页 PDF）。"""
    if not is_image(path):
        return fitz.open(path)
    try:
        return fitz.open("pdf", _image_to_pdf_bytes(path))
    except Exception:
        try:
            from PIL import Image
        except Exception as exc:
            raise RuntimeError("无法识别图片格式（内置转换失败，且缺少 Pillow）：%s" % exc)
        import tempfile
        im = Image.open(path)
        dpi = im.info.get("dpi") or (96, 96)
        if isinstance(dpi, (int, float)):
            dpi = (dpi, dpi)
        tmpdir = tempfile.mkdtemp(prefix="pdfbatch_img_")
        png = os.path.join(tmpdir, "image.png")
        try:
            im.convert("RGB").save(png, dpi=(float(dpi[0]) or 96.0, float(dpi[1]) or 96.0) if isinstance(dpi, tuple) else (96.0, 96.0))
            return fitz.open("pdf", _image_to_pdf_bytes(png))
        finally:
            try:
                import shutil
                shutil.rmtree(tmpdir, ignore_errors=True)
            except Exception:
                pass


def emit(line):
    sys.stdout.write(line + "\n")


def mm_to_pt(mm):
    return mm / 25.4 * 72.0


def cmd_info(paths):
    for path in paths:
        try:
            doc = open_doc(path)
        except Exception as exc:
            emit("E\t%s\t%s" % (path, str(exc).replace("\t", " ")))
            continue
        n = doc.page_count
        emit("F\t%s\t%d" % (path, n))
        for i in range(n):
            rect = doc[i].rect
            w_mm = rect.width / 72.0 * 25.4
            h_mm = rect.height / 72.0 * 25.4
            emit("P\t%.1f\t%.1f" % (w_mm, h_mm))
        doc.close()


def cmd_render(path, outdir, dpi, gray):
    os.makedirs(outdir, exist_ok=True)
    doc = open_doc(path)
    stem = os.path.splitext(os.path.basename(path))[0]
    colorspace = fitz.csGRAY if gray == "1" else fitz.csRGB
    for i in range(doc.page_count):
        pix = doc[i].get_pixmap(dpi=int(dpi), colorspace=colorspace, alpha=False)
        out = os.path.join(outdir, "p%04d.png" % (i + 1))
        pix.save(out)
        emit("P\t%d\t%s" % (i + 1, out))
    doc.close()


def _union(box, r):
    """矩形并集：手工合并，保证零宽/零高的线也能撑大内容框（fitz 的 a|b 会忽略这类矩形）。"""
    if box is None:
        return fitz.Rect(r)
    return fitz.Rect(min(box.x0, r.x0), min(box.y0, r.y0),
                     max(box.x1, r.x1), max(box.y1, r.y1))


def content_box(page):
    """内容框：文字块 ∪ 矢量图形 ∪ 图片 的并集；异常或过小时回退整页。"""
    pr = page.rect
    box = None
    try:
        for b in page.get_text("blocks"):
            box = _union(box, fitz.Rect(b[0], b[1], b[2], b[3]))
    except Exception:
        pass
    try:
        for d in page.get_drawings():
            r = d.get("rect")
            if r is None:
                continue
            r = fitz.Rect(r)
            try:
                w = float(d.get("width") or 0.0)
            except Exception:
                w = 0.0
            if w > 0:
                r = fitz.Rect(r.x0 - w / 2.0, r.y0 - w / 2.0, r.x1 + w / 2.0, r.y1 + w / 2.0)
            box = _union(box, r)
    except Exception:
        pass
    try:
        for info in page.get_image_info():
            box = _union(box, fitz.Rect(info["bbox"]))
    except Exception:
        pass
    if box is None:
        return pr
    box = box & pr
    if box.is_empty or box.width < 1 or box.height < 1:
        return pr
    box = fitz.Rect(box.x0 - CROP_PAD, box.y0 - CROP_PAD,
                    box.x1 + CROP_PAD, box.y1 + CROP_PAD) & pr
    if box.is_empty or box.width < 1 or box.height < 1:
        return pr
    if box.width * box.height < 0.02 * pr.width * pr.height:
        return pr
    return box


def fit_rect(box, cell, pad):
    avail_w = max(1.0, cell.width - pad * 2.0)
    avail_h = max(1.0, cell.height - pad * 2.0)
    scale = min(avail_w / float(box.width), avail_h / float(box.height))
    w = box.width * scale
    h = box.height * scale
    x = cell.x0 + (cell.width - w) / 2.0
    y = cell.y0 + (cell.height - h) / 2.0
    return fitz.Rect(x, y, x + w, y + h)


def cell_rects(page_rect, nup):
    w = page_rect.width
    h = page_rect.height
    if nup == 2:
        half = h / 2.0
        return [fitz.Rect(0, 0, w, half), fitz.Rect(0, half, w, h)]
    hw = w / 2.0
    hh = h / 2.0
    return [
        fitz.Rect(0, 0, hw, hh),
        fitz.Rect(hw, 0, w, hh),
        fitz.Rect(0, hh, hw, h),
        fitz.Rect(hw, hh, w, h),
    ]


def read_list(list_path):
    paths = []
    with open(list_path, "r", encoding="utf-8-sig") as f:
        for line in f:
            p = line.strip()
            if p and not p.startswith("#"):
                paths.append(p)
    return paths


def cmd_merge(out_path, nup, crop, paper, list_path):
    try:
        nup = int(nup)
    except Exception:
        sys.stderr.write("nup 必须为 1/2/4\n")
        return 2
    if nup not in (1, 2, 4):
        sys.stderr.write("nup 只支持 1/2/4\n")
        return 2
    crop = (crop == "1")
    pw, ph = PAPERS.get(str(paper).upper(), PAPERS["A4"])
    paths = read_list(list_path)
    if not paths:
        sys.stderr.write("列表文件里没有 PDF 路径\n")
        return 2
    outdir = os.path.dirname(os.path.abspath(out_path))
    if outdir:
        os.makedirs(outdir, exist_ok=True)

    pad = mm_to_pt(PAD_MM)
    page_rect = fitz.Rect(0, 0, pw, ph)
    cells = cell_rects(page_rect, nup) if nup > 1 else None
    out = fitz.open()
    cur_page = None
    slot = 0
    placed = 0
    for path in paths:
        try:
            src = open_doc(path)
        except Exception as exc:
            sys.stderr.write("跳过无法打开的文件: %s (%s)\n" % (path, exc))
            continue
        try:
            if nup == 1 and not crop:
                out.insert_pdf(src)
                placed += src.page_count
                continue
            for i in range(src.page_count):
                spage = src[i]
                box = content_box(spage) if crop else spage.rect
                if nup == 1:
                    page = out.new_page(width=box.width, height=box.height)
                    page.show_pdf_page(fitz.Rect(0, 0, box.width, box.height), src, i, clip=box)
                else:
                    if cur_page is None or slot >= nup:
                        cur_page = out.new_page(width=pw, height=ph)
                        slot = 0
                    target = fit_rect(box, cells[slot], pad)
                    cur_page.show_pdf_page(target, src, i, clip=box)
                    slot += 1
                placed += 1
        finally:
            src.close()

    if out.page_count == 0:
        out.close()
        sys.stderr.write("没有可合并的页面\n")
        return 1
    out.save(out_path, garbage=3, deflate=True)
    pages = out.page_count
    out.close()
    emit("M\t%s\t%d" % (out_path, pages))
    sys.stderr.write("合并 %d 页（%d 张发票）\n" % (pages, placed))
    return 0


def cmd_img2pdf(out_path, paths):
    outdir = os.path.dirname(os.path.abspath(out_path))
    if outdir:
        os.makedirs(outdir, exist_ok=True)
    out = fitz.open()
    placed = 0
    for path in paths:
        try:
            src = open_doc(path)
        except Exception as exc:
            sys.stderr.write("跳过无法打开的文件: %s (%s)\n" % (path, exc))
            continue
        try:
            out.insert_pdf(src)
            placed += src.page_count
        finally:
            src.close()
    if out.page_count == 0:
        out.close()
        sys.stderr.write("没有可转换的图片\n")
        return 1
    out.save(out_path, garbage=3, deflate=True)
    pages = out.page_count
    out.close()
    emit("M\t%s\t%d" % (out_path, pages))
    sys.stderr.write("图片转 PDF 完成：%d 页\n" % pages)
    return 0


def main():
    if len(sys.argv) < 2:
        sys.stderr.write(__doc__)
        return 2
    cmd = sys.argv[1]
    if cmd == "info":
        cmd_info(sys.argv[2:])
        return 0
    if cmd == "render":
        if len(sys.argv) < 6:
            sys.stderr.write("render 需要 4 个参数\n")
            return 2
        cmd_render(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5])
        return 0
    if cmd == "merge":
        if len(sys.argv) < 7:
            sys.stderr.write("merge 需要 5 个参数\n")
            return 2
        return cmd_merge(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6])
    if cmd == "img2pdf":
        if len(sys.argv) < 4:
            sys.stderr.write("img2pdf 需要至少 2 个参数\n")
            return 2
        return cmd_img2pdf(sys.argv[2], sys.argv[3:])
    sys.stderr.write("未知命令: %s\n" % cmd)
    return 2


if __name__ == "__main__":
    sys.exit(main())
