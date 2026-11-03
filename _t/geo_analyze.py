from PIL import Image
import os
base = 'C:/Windows/Temp/pdfbatch_test/probe/'
def analyze(p):
    im = Image.open(p).convert('RGB')
    w,h = im.size
    px = im.load()
    def bbox(pred):
        minx,miny,maxx,maxy = w,h,-1,-1
        step = 6
        for y in range(0,h,step):
            for x in range(0,w,step):
                r,g,b = px[x,y]
                if pred(r,g,b):
                    if x<minx: minx=x
                    if x>maxx: maxx=x
                    if y<miny: miny=y
                    if y>maxy: maxy=y
        return None if maxx<0 else (minx,miny,maxx,maxy)
    red = bbox(lambda r,g,b: r>200 and g<80 and b<80)
    green = bbox(lambda r,g,b: g>200 and r<80 and b<80)
    blue = bbox(lambda r,g,b: b>200 and r<80 and g<80)
    print(os.path.basename(p), 'size=%dx%d' % (w,h))
    for name,bb in (('red',red),('green',green),('blue',blue)):
        if bb: print('   %-6s px=(%d,%d)-(%d,%d)' % (name,bb[0],bb[1],bb[2],bb[3]))
        else: print('   %-6s none' % name)
    print('   corners TL=%s TR=%s BL=%s BR=%s' % (px[2,2], px[w-3,2], px[2,h-3], px[w-3,h-3]))
for f in ['geo_m433_land_m.png','geo_pdf_land_m.png']:
    analyze(base + f)