
import fitz, glob, os
d = r'C:\Users\Administrator\Desktop\Solidworks图标'
files = sorted(glob.glob(os.path.join(d, '*.pdf')))
for f in files:
    try:
        doc = fitz.open(f)
    except Exception as e:
        print('ERR', os.path.basename(f), e); continue
    pg = doc[0]
    r = pg.rect
    # content bbox via drawings + text + images
    x0,y0,x1,y1 = r
    rects=[]
    for b in pg.get_text('blocks'):
        rects.append(fitz.Rect(b[:4]))
    try:
        for dr in pg.get_drawings():
            rects.append(dr['rect'])
    except Exception: pass
    for im in pg.get_image_info():
        rects.append(fitz.Rect(im['bbox']))
    cb = fitz.Rect()
    for rr in rects:
        if rr.is_empty or rr.is_infinite: continue
        cb |= rr
    cb = cb & r
    mm = lambda v: v/72*25.4
    print('%-52s pages=%d  page=%.0fx%.0fmm  content=%.0fx%.0fmm  bbox=(%.0f,%.0f,%.0f,%.0f)mm' % (
        os.path.basename(f)[:52], doc.page_count, mm(r.width), mm(r.height),
        mm(cb.width), mm(cb.height), mm(cb.x0), mm(cb.y0), mm(cb.x1), mm(cb.y1)))
    doc.close()
