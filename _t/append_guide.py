
import io, os
p = r'C:\Users\<用户名>\Desktop\PDF批量打印工具\使用说明.txt'
raw = open(p, 'rb').read()
bom = raw.startswith(b'\xef\xbb\xbf')
text = raw.decode('utf-8-sig')
add = """
【拷到别的电脑上用（便携版）】
- 整个文件夹一起拷，不要只拷 exe：PdfBatchPrint.exe、render_pdf.py、python\ 三个都在才行。
- 目标电脑：Windows 7 SP1 及以上；需要系统自带 .NET Framework 4.x（Win10/11 默认已有，Win7 可能要装）。
- 在目标电脑上先装好打印机驱动并添加打印机（共享打印机用 \\服务器名\打印机名，例如 \\打印服务器\HP MFP M433 PCL 6），
  程序会把本机那个占位端口的同名打印机标成「[本机占位端口，打不出纸]」，并自动改用共享的那台。
- python\ 是随包的精简 Python 3.12 + PyMuPDF，只服务本程序，不需要安装、不写注册表、不影响电脑上已有的 Python。
  日志里出现「渲染引擎就绪: ...\python\python.exe」就说明用的是内置环境。
- 建议先拷到本地硬盘（例如 D:\PDF批量打印工具）再运行；直接从 U 盘/网络盘运行会慢一些。
- 设置存在 %APPDATA%\PdfBatchPrint\settings.ini，换电脑后是新的默认值，重新选一次打印机即可。
"""
if '便携版' not in text:
    text = text.rstrip('\r\n') + '\r\n' + add.replace('\n', '\r\n')
    with open(p, 'wb') as f:
        f.write(('\ufeff' if bom else '').encode('utf-8') + text.encode('utf-8'))
    print('appended; bom=', bom)
else:
    print('already present')
print('lines now', text.count('\n')+1)
