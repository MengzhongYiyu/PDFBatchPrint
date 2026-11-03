import fitz, os
src='C:/Users/Administrator/Desktop/PDF批量打印工具/预览_CQ00001_A4.pdf'
d=fitz.open(src); pg=d[0]
print('page', round(pg.rect.width,1), round(pg.rect.height,1))
info=pg.get_image_info()[0]['bbox']
u=lambda v: v/72*100
print('img y %.1f..%.1f  gaps top=%.1f bottom=%.1f u' % (u(info[1]),u(info[3]),u(info[1]), u(pg.rect.height)-u(info[3])))
pix=pg.get_pixmap(dpi=110)
out='D:/laji/pdfbatch/_t/preview_a4_margin1.png'
pix.save(out)
print('saved', out, pix.width, pix.height)