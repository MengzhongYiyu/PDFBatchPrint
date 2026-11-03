import fitz, glob, os
tot=0
for p in sorted(glob.glob('C:/Windows/Temp/pdfbatch_test/out3/*.pdf')):
    d = fitz.open(p); pg = d[0]
    infos = pg.get_image_info(xrefs=True)
    if not infos: print(os.path.basename(p), 'no image'); continue
    b = infos[0]['bbox']
    # pt -> 1/100 inch
    u = lambda v: v/72*100
    print('%-24s page %dx%d u | img y %.1f..%.1f u  bottom gap=%.1f u (%.2f mm)  right gap=%.1f u' % (
        os.path.basename(p)[:24], round(u(pg.rect.width)), round(u(pg.rect.height)), u(b[1]), u(b[3]),
        u(pg.rect.height)-u(b[3]), (u(pg.rect.height)-u(b[3]))/100*25.4, u(pg.rect.width)-u(b[2])))
    tot+=1
print('checked', tot)