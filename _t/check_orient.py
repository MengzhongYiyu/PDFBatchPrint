import fitz, glob, os
for p in [r'C:\Windows\Temp\pdfbatch_test\o_portrait.pdf', r'C:\Windows\Temp\pdfbatch_test\o_a3.pdf',
          r'C:\Windows\Temp\pdfbatch_test\o_forced.pdf', r'C:\Windows\Temp\pdfbatch_test\o_a3_portrait.pdf']:
    if not os.path.exists(p):
        print(os.path.basename(p), 'MISSING'); continue
    d = fitz.open(p)
    r = d[0].rect
    print('%-22s %.1f x %.1f mm  %s' % (os.path.basename(p), r.width/72*25.4, r.height/72*25.4, 'landscape' if r.width > r.height else 'portrait'))
    d.close()