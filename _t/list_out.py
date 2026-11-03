import fitz, glob, os
for p in sorted(glob.glob(r'C:\Windows\Temp\pdfbatch_test\out2\*.pdf')):
    d = fitz.open(p); r = d[0].rect
    print('%-30s %.0f x %.0f mm %s' % (os.path.basename(p)[:30], r.width/72*25.4, r.height/72*25.4, 'landscape' if r.width>r.height else 'portrait')); d.close()