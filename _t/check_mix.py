import fitz, os, sys
base = r'C:\Windows\Temp\pdfbatch_test\mix'
for n in ['a.pdf','b.pdf']:
    p = os.path.join(base, n)
    if not os.path.exists(p): print(n, 'MISSING'); continue
    d = fitz.open(p); r = d[0].rect
    print('%-8s %.1f x %.1f mm %s' % (n, r.width/72*25.4, r.height/72*25.4, 'landscape' if r.width>r.height else 'portrait')); d.close()