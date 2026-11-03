import fitz
doc = fitz.open()
p = doc.new_page(width=595, height=842)  # A4 portrait pt
p.insert_text((100, 200), 'PORTRAIT TEST', fontsize=36)
p.draw_rect(fitz.Rect(60, 60, 535, 782), color=(0,0,0), width=2)
doc.save(r'D:\laji\pdfbatch\_t\portrait_test.pdf')
doc.close()
print('ok')