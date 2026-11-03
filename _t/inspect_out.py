import fitz
d = fitz.open(r'C:\Windows\Temp\pdfbatch_test\ck_a3toa4.pdf')
p = d[0]
print('page pt: %.1f x %.1f  (mm %.1f x %.1f)' % (p.rect.width, p.rect.height, p.rect.width/72*25.4, p.rect.height/72*25.4))
for i in p.get_image_info(xrefs=True):
    b = i['bbox']
    print('image bbox pt: (%.1f, %.1f) - (%.1f, %.1f)' % (b[0], b[1], b[2], b[3]))
    print('  w=%.1f h=%.1f  bottom_overflow=%.1f  right_overflow=%.1f' % (b[2]-b[0], b[3]-b[1], b[3]-p.rect.height, b[2]-p.rect.width))
pix = p.get_pixmap(dpi=72)
pix.save(r'C:\Windows\Temp\pdfbatch_test\ck_a3toa4.png')
print('saved')