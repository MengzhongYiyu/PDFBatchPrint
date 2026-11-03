using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PdfBatchPrint
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--cli", StringComparison.OrdinalIgnoreCase) ||
                (args.Length > 0 && (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase))))
            {
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
                if (string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase))
                {
                    Cli.Usage();
                    return;
                }
                Environment.ExitCode = Cli.Run(args);
                return;
            }
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var startFiles = args.Where(a => !string.IsNullOrEmpty(a) && a[0] != '-').ToArray();
            Application.Run(new MainForm(startFiles));
        }
    }

    internal static class AppPaths
    {
        public static string ExeDir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }
        public static string ScriptPath { get { return Path.Combine(ExeDir, "render_pdf.py"); } }
        public static string DataDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdfBatchPrint");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.ini"); } }
    }

    internal sealed class Settings
    {
        public string Printer = "";
        public string Engine = "render";
        public int Copies = 1;
        public string Duplex = "simplex";
        public string Paper = "auto";
        public string Orientation = "auto";
        public string Scale = "fit";
        public int Dpi = 300;
        public double MarginMm = 1.0;
        public bool Gray = false;
        public string Merge = "off";
        public bool CropInvoice = false;
        public string PythonPath = "";
        public int FormWidth = 1100;
        public int FormHeight = 800;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(AppPaths.SettingsFile)) return s;
                foreach (string raw in File.ReadAllLines(AppPaths.SettingsFile, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "Printer": s.Printer = val; break;
                        case "Engine": s.Engine = val; break;
                        case "Copies": s.Copies = Math.Max(1, ParseInt(val, 1)); break;
                        case "Duplex": s.Duplex = val; break;
                        case "Paper": s.Paper = val; break;
                        case "Orientation": s.Orientation = val; break;
                        case "Merge": s.Merge = val; break;
                        case "CropInvoice": s.CropInvoice = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                        case "Scale": s.Scale = val; break;
                        case "Dpi": s.Dpi = ParseInt(val, 300); break;
                        case "MarginMm": s.MarginMm = ParseDouble(val, 1.0); break;
                        case "Gray": s.Gray = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                        case "PythonPath": s.PythonPath = val; break;
                        case "FormWidth": s.FormWidth = ParseInt(val, 1020); break;
                        case "FormHeight": s.FormHeight = ParseInt(val, 740); break;
                    }
                }
            }
            catch { }
            return s;
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static double ParseDouble(string s, double fallback)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# PdfBatchPrint 设置");
                sb.AppendLine("Printer=" + Printer);
                sb.AppendLine("Engine=" + Engine);
                sb.AppendLine("Copies=" + Copies);
                sb.AppendLine("Duplex=" + Duplex);
                sb.AppendLine("Paper=" + Paper);
                sb.AppendLine("Orientation=" + Orientation);
                sb.AppendLine("Merge=" + Merge);
                sb.AppendLine("CropInvoice=" + (CropInvoice ? "1" : "0"));
                sb.AppendLine("Scale=" + Scale);
                sb.AppendLine("Dpi=" + Dpi);
                sb.AppendLine("MarginMm=" + MarginMm.ToString("0.#", CultureInfo.InvariantCulture));
                sb.AppendLine("Gray=" + (Gray ? "1" : "0"));
                sb.AppendLine("PythonPath=" + PythonPath);
                sb.AppendLine("FormWidth=" + FormWidth);
                sb.AppendLine("FormHeight=" + FormHeight);
                File.WriteAllText(AppPaths.SettingsFile, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }

    internal sealed class PdfPageInfo
    {
        public double WidthMm;
        public double HeightMm;
        public string SizeLabel
        {
            get { return string.Format(CultureInfo.InvariantCulture, "{0:0}x{1:0}mm", WidthMm, HeightMm); }
        }
    }

    internal sealed class PdfFileInfo
    {
        public string Path = "";
        public readonly List<PdfPageInfo> Pages = new List<PdfPageInfo>();
        public string Error = "";
        public bool Ok { get { return Error.Length == 0 && Pages.Count > 0; } }
    }

    internal static class PdfEngine
    {
        public static string ResolvePython(string configured)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured.Trim());
            string env = Environment.GetEnvironmentVariable("PDFBATCH_PYTHON");
            if (!string.IsNullOrWhiteSpace(env)) candidates.Add(env.Trim());
            candidates.Add(Path.Combine(AppPaths.ExeDir, @"python\python.exe"));
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, @".dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"));
            candidates.Add(@"C:\Python312\python.exe");
            candidates.Add(@"C:\Python311\python.exe");
            foreach (string p in PathCandidates("python.exe")) candidates.Add(p);

            foreach (string c in candidates)
            {
                try
                {
                    if (!File.Exists(c)) continue;
                    if (TestPymupdf(c)) return c;
                }
                catch { }
            }
            return null;
        }

        private static IEnumerable<string> PathCandidates(string exe)
        {
            var list = new List<string>();
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string dir in path.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    try
                    {
                        string full = Path.Combine(dir.Trim(), exe);
                        if (File.Exists(full)) list.Add(full);
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        private static bool TestPymupdf(string python)
        {
            try
            {
                var psi = new ProcessStartInfo(python, "-c \"import pymupdf\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        public static string Run(string python, string arguments, int timeoutMs)
        {
            var psi = new ProcessStartInfo(python, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var p = Process.Start(psi))
            {
                if (p == null) throw new InvalidOperationException("无法启动 Python");
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("Python 处理超时");
                }
                if (p.ExitCode != 0)
                {
                    throw new InvalidOperationException("Python 出错: " + stderr.Replace("\r", " ").Replace("\n", " "));
                }
                return stdout;
            }
        }

        public static List<PdfFileInfo> GetInfo(string python, IList<string> files)
        {
            var results = new List<PdfFileInfo>();
            if (files.Count == 0) return results;
            var script = AppPaths.ScriptPath;
            if (!File.Exists(script)) throw new FileNotFoundException("缺少 render_pdf.py: " + script);
            foreach (var chunk in Chunk(files, 20))
            {
                string args = Quote(script) + " info " + string.Join(" ", chunk.Select(Quote).ToArray());
                string stdout = Run(python, args, 600000);
                PdfFileInfo current = null;
                foreach (string raw in stdout.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    if (line.Length == 0) continue;
                    string[] parts = line.Split('\t');
                    if (parts[0] == "F" && parts.Length >= 3)
                    {
                        current = new PdfFileInfo { Path = parts[1] };
                        int n;
                        int.TryParse(parts[2], out n);
                        results.Add(current);
                    }
                    else if (parts[0] == "P" && parts.Length >= 3 && current != null)
                    {
                        current.Pages.Add(new PdfPageInfo
                        {
                            WidthMm = double.Parse(parts[1], CultureInfo.InvariantCulture),
                            HeightMm = double.Parse(parts[2], CultureInfo.InvariantCulture)
                        });
                    }
                    else if (parts[0] == "E" && parts.Length >= 3)
                    {
                        results.Add(new PdfFileInfo { Path = parts[1], Error = parts[2] });
                    }
                }
            }
            return results;
        }

        public static List<string> Render(string python, string pdf, string outDir, int dpi, bool gray)
        {
            var script = AppPaths.ScriptPath;
            string args = Quote(script) + " render " + Quote(pdf) + " " + Quote(outDir) + " " + dpi.ToString(CultureInfo.InvariantCulture) + " " + (gray ? "1" : "0");
            string stdout = Run(python, args, 1800000);
            var list = new List<string>();
            foreach (string raw in stdout.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                string[] parts = line.Split('\t');
                if (parts[0] == "P" && parts.Length >= 3) list.Add(parts[2]);
            }
            if (list.Count == 0) throw new InvalidOperationException("渲染失败，未生成任何页面");
            return list;
        }

        public static string Merge(string python, IList<string> files, string outPdf, string nup, bool crop, string paper)
        {
            var script = AppPaths.ScriptPath;
            if (!File.Exists(script)) throw new FileNotFoundException("缺少 render_pdf.py: " + script);
            string listPath = outPdf + ".list.txt";
            var sb = new StringBuilder();
            foreach (string f in files) sb.AppendLine(f);
            File.WriteAllText(listPath, sb.ToString(), new UTF8Encoding(false));
            try
            {
                string args = Quote(script) + " merge " + Quote(outPdf) + " " + nup + " " + (crop ? "1" : "0") + " " + paper + " " + Quote(listPath);
                string stdout = Run(python, args, 1800000);
                string result = outPdf;
                foreach (string rawLine in stdout.Split('\n'))
                {
                    string line = rawLine.TrimEnd('\r');
                    if (line.Length == 0) continue;
                    string[] parts = line.Split('\t');
                    if (parts[0] == "M" && parts.Length >= 3) result = parts[1];
                }
                if (!File.Exists(result)) throw new InvalidOperationException("合并失败，未生成文件");
                return result;
            }
            finally
            {
                try { File.Delete(listPath); } catch { }
            }
        }

        public static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".jpe", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".pnm", ".pgm", ".ppm", ".pcx", ".tga", ".psd", ".jxr" };

        public const string SupportedFilter =
            "支持的文件 (*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp"
            + "|PDF 文件 (*.pdf)|*.pdf"
            + "|图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp"
            + "|所有文件 (*.*)|*.*";

        public static bool IsImagePath(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return false;
            foreach (string e in ImageExtensions)
                if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool IsSupportedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase) || IsImagePath(path);
        }

        public static List<string> CollectFiles(string directory)
        {
            var list = new List<string>();
            foreach (string f in Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
                if (IsSupportedPath(f)) list.Add(f);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        public static string Img2Pdf(string python, string imagePath, string outPdf)
        {
            var script = AppPaths.ScriptPath;
            if (!File.Exists(script)) throw new FileNotFoundException("缺少 render_pdf.py: " + script);
            string args = Quote(script) + " img2pdf " + Quote(outPdf) + " " + Quote(imagePath);
            string stdout = Run(python, args, 600000);
            string result = outPdf;
            foreach (string rawLine in stdout.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0) continue;
                string[] parts = line.Split('\t');
                if (parts[0] == "M" && parts.Length >= 2) result = parts[1];
            }
            if (!File.Exists(result)) throw new InvalidOperationException("图片转 PDF 失败");
            return result;
        }

        private static IEnumerable<List<string>> Chunk(IList<string> source, int size)
        {
            var buffer = new List<string>();
            foreach (string item in source)
            {
                buffer.Add(item);
                if (buffer.Count == size) { yield return buffer; buffer = new List<string>(); }
            }
            if (buffer.Count > 0) yield return buffer;
        }

        public static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class PrintOptions
    {
        public string Printer = "";
        public string Engine = "render";
        public int Copies = 1;
        public Duplex Duplex = Duplex.Default;
        public string Paper = "auto";
        public string Orientation = "auto";
        public string Scale = "fit";
        public int Dpi = 300;
        public double MarginMm = 1.0;
        public bool Gray = false;
        public string Merge = "off";
        public bool CropInvoice = false;
        public string SaveMerged = "";
        public bool PrintToFile = false;
        public string PrintFileName = "";
        public string Python = "";
    }

    internal sealed class BatchPrinter
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDefaultPrinter(string name);

        public event Action<string> Log;
        public event Action<int, int, string> Progress;
        public int FailedCount;
        public volatile bool Cancelled;

        private void Emit(string message)
        {
            var h = Log;
            if (h != null) h(message);
        }

        private void Report(int done, int total, string message)
        {
            var h = Progress;
            if (h != null) h(done, total, message);
        }

        public static bool MergeActive(PrintOptions opt)
        {
            return !string.IsNullOrEmpty(opt.Merge) && !opt.Merge.Equals("off", StringComparison.OrdinalIgnoreCase);
        }

        internal static string NupValue(string merge)
        {
            if (merge.Equals("1up", StringComparison.OrdinalIgnoreCase)) return "1";
            if (merge.Equals("4up", StringComparison.OrdinalIgnoreCase)) return "4";
            return "2";
        }

        private static string NupLabel(string merge)
        {
            if (merge.Equals("1up", StringComparison.OrdinalIgnoreCase)) return "每页 1 张";
            if (merge.Equals("4up", StringComparison.OrdinalIgnoreCase)) return "每页 4 张";
            return "每页 2 张";
        }

        internal static string MergePaper(string paper)
        {
            return string.Equals(paper, "A3", StringComparison.OrdinalIgnoreCase) ? "A3" : "A4";
        }

        public void Run(IList<string> files, PrintOptions opt)
        {
            int total = files.Count;
            int done = 0;
            var failures = new List<string>();
            IList<string> work = files;
            string mergeTmp = null;
            try
            {
                if (MergeActive(opt) && total > 0)
                {
                    if (string.IsNullOrEmpty(opt.Python))
                        throw new InvalidOperationException("合并发票需要 Python + PyMuPDF");
                    Report(0, 1, "正在合并 " + total + " 个文件 ...");
                    Emit("合并: " + total + " 个文件 → " + MergePaper(opt.Paper) + "，" + NupLabel(opt.Merge)
                        + (opt.CropInvoice ? "，裁掉白边" : ""));
                    mergeTmp = Path.Combine(TempRoot(), Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(mergeTmp);
                    string target = Path.Combine(mergeTmp, "merged.pdf");
                    string merged = PdfEngine.Merge(opt.Python, files, target, NupValue(opt.Merge), opt.CropInvoice, MergePaper(opt.Paper));
                    var mInfos = PdfEngine.GetInfo(opt.Python, new List<string> { merged });
                    int mPages = mInfos.Count > 0 ? mInfos[0].Pages.Count : 0;
                    Emit("合并完成: " + mPages + " 页");
                    if (!string.IsNullOrEmpty(opt.SaveMerged))
                    {
                        try { File.Copy(merged, opt.SaveMerged, true); Emit("已另存合并文件: " + opt.SaveMerged); }
                        catch (Exception saveEx) { Emit("另存失败: " + saveEx.Message); }
                    }
                    work = new List<string> { merged };
                    total = 1;
                    done = 0;
                }
                for (int i = 0; i < total; i++)
                {
                    if (Cancelled) break;
                    string file = work[i];
                    string name = Path.GetFileName(file);
                    Report(done, total, "正在打印 " + name + " ...");
                    Emit("开始: " + file);
                    try
                    {
                        if (string.Equals(opt.Engine, "wps", StringComparison.OrdinalIgnoreCase))
                        {
                            string target = file;
                            string converted = null;
                            if (PdfEngine.IsImagePath(file))
                            {
                                if (string.IsNullOrEmpty(opt.Python))
                                    throw new InvalidOperationException("用 WPS 引擎打印图片需要 Python + PyMuPDF");
                                converted = Path.Combine(TempRoot(), Guid.NewGuid().ToString("N") + ".pdf");
                                PdfEngine.Img2Pdf(opt.Python, file, converted);
                                target = converted;
                                Emit("  图片已转为临时 PDF: " + Path.GetFileName(converted));
                            }
                            try { PrintViaWps(target, opt); }
                            finally { if (converted != null) { try { File.Delete(converted); } catch { } } }
                        }
                        else
                            PrintViaRender(file, opt);
                        done++;
                        Emit("完成: " + name);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(name + ": " + ex.Message);
                        Emit("失败: " + name + " -> " + ex.ToString());
                    }
                    Report(done, total, "已完成 " + done + "/" + total);
                }
                FailedCount = failures.Count;
                Report(done, total, Cancelled ? "已停止" : "结束");
                if (failures.Count > 0)
                    Emit("汇总: 成功 " + done + " 个，失败 " + failures.Count + " 个 -> " + string.Join(" | ", failures.ToArray()));
                else
                    Emit("汇总: 全部成功 " + done + " 个文件");
            }
            finally
            {
                if (mergeTmp != null) { try { Directory.Delete(mergeTmp, true); } catch { } }
            }
        }

        private static int EffectiveDpi(int requested, PdfPageInfo page)
        {
            double wIn = page.WidthMm / 25.4;
            double hIn = page.HeightMm / 25.4;
            double pixels = wIn * hIn * requested * (double)requested;
            const double limit = 45.0 * 1000 * 1000;
            if (pixels <= limit) return requested;
            int dpi = (int)Math.Floor(Math.Sqrt(limit / (wIn * hIn)));
            return Math.Max(100, Math.Min(requested, dpi));
        }

        private string _tempRoot;

        private string TempRoot()
        {
            if (_tempRoot != null) return _tempRoot;
            var candidates = new List<string>();
            try { candidates.Add(Path.Combine(Path.GetTempPath(), "PdfBatchPrint")); } catch { }
            try { candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfBatchPrint", "temp")); } catch { }
            try { candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp")); } catch { }
            foreach (string c in candidates)
            {
                try
                {
                    string probe = Path.Combine(c, "probe_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(probe);
                    Directory.Delete(probe, true);
                    _tempRoot = c;
                    return c;
                }
                catch { }
            }
            _tempRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            try { Directory.CreateDirectory(_tempRoot); } catch { }
            return _tempRoot;
        }

        private void PrintViaRender(string file, PrintOptions opt)
        {
            if (string.IsNullOrEmpty(opt.Python))
                throw new InvalidOperationException("未找到可用的 Python + PyMuPDF，无法使用精确渲染引擎");

            var infos = PdfEngine.GetInfo(opt.Python, new List<string> { file });
            if (infos.Count == 0) throw new InvalidOperationException("读取 PDF 信息失败");
            var info = infos[0];
            if (!string.IsNullOrEmpty(info.Error)) throw new InvalidOperationException(info.Error);

            PdfPageInfo firstPage = info.Pages[0];
            Emit("  页面 " + firstPage.SizeLabel + " → " + (ResolveLandscape(opt.Orientation, firstPage) ? "横向" : "纵向")
                + " / " + PaperLabel(opt.Paper, firstPage));

            string tmp = Path.Combine(TempRoot(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                // render 会一次性把整个 PDF 的所有页都渲染出来，这里只能调用一次；
                // 取所有页里最保守的 dpi，保证任何一页都不超过像素上限。
                int dpi = opt.Dpi;
                foreach (var page in info.Pages) dpi = Math.Min(dpi, EffectiveDpi(opt.Dpi, page));
                var images = PdfEngine.Render(opt.Python, file, tmp, dpi, opt.Gray);
                var dims = new List<PdfPageInfo>();
                for (int i = 0; i < images.Count; i++)
                    dims.Add(info.Pages[Math.Min(i, info.Pages.Count - 1)]);
                PrintImages(file, images, dims, opt);
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
            }
        }

        private void PrintImages(string sourceFile, List<string> images, List<PdfPageInfo> dims, PrintOptions opt)
        {
            int index = 0;
            var doc = new PrintDocument();
            doc.DocumentName = Path.GetFileNameWithoutExtension(sourceFile);
            doc.PrintController = new StandardPrintController();
            doc.PrinterSettings.PrinterName = opt.Printer;
            doc.PrinterSettings.Copies = (short)Math.Max(1, opt.Copies);
            doc.PrinterSettings.Collate = true;
            if (opt.Duplex != Duplex.Default) doc.PrinterSettings.Duplex = opt.Duplex;
            if (opt.PrintToFile)
            {
                doc.PrinterSettings.PrintToFile = true;
                doc.PrinterSettings.PrintFileName = opt.PrintFileName;
            }
            if (!doc.PrinterSettings.IsValid) throw new InvalidOperationException("打印机不可用: " + opt.Printer);

            doc.QueryPageSettings += delegate(object sender, QueryPageSettingsEventArgs e)
            {
                if (dims.Count == 0) return;
                var page = dims[index % dims.Count];
                try
                {
                    e.PageSettings.PaperSize = ResolvePaper(opt.Paper, page);
                    e.PageSettings.Landscape = ResolveLandscape(opt.Orientation, page);
                    int hx = (int)Math.Ceiling(e.PageSettings.HardMarginX);
                    int hy = (int)Math.Ceiling(e.PageSettings.HardMarginY);
                    hx = Math.Max(0, Math.Min(200, hx));
                    hy = Math.Max(0, Math.Min(200, hy));
                    e.PageSettings.Margins = new Margins(hx, hx, hy, hy);
                }
                catch { }
            };

            doc.PrintPage += delegate(object sender, PrintPageEventArgs e)
            {
                if (Cancelled) { e.HasMorePages = false; return; }
                if (index >= images.Count) { e.HasMorePages = false; return; }
                using (var img = Image.FromFile(images[index]))
                {
                    if (index == 0)
                    {
                        RectangleF pa = PrintableArea(e, opt);
                        Emit("  可打印区 " + Math.Round(pa.Width) + "x" + Math.Round(pa.Height)
                            + "（1/100 英寸），安全边距 " + opt.MarginMm.ToString("0.#", CultureInfo.InvariantCulture)
                            + "mm，纸张 " + e.PageSettings.PaperSize.PaperName + " / "
                            + (e.PageSettings.Landscape ? "横向" : "纵向"));
                    }
                    RectangleF target = FitRect(e, img, dims[index], opt);
                    e.Graphics.DrawImage(img, target);
                }
                index++;
                e.HasMorePages = index < images.Count;
            };

            doc.Print();
        }

        // 计算"可打印区"（单位 1/100 英寸，原点为可打印区左上角）。
        // 关键：StandardPrintController 下 PrintPageEventArgs.Graphics 的原点位于可打印区左上角
        // （OriginAtMargins=false），而 MarginBounds 是以整页为基准的矩形，两者相差一个硬边距。
        // 旧代码直接用 MarginBounds.X/Y 定位，等于把内容整体下移/右移了一个硬边距，
        // A3 缩到 A4 时就会把最下面一条推出可打印区、被驱动裁掉。这里统一按"可打印区尺寸"从 (0,0) 排版。
        private static RectangleF PrintableArea(PrintPageEventArgs e, PrintOptions opt)
        {
            Rectangle mb = e.MarginBounds;
            float hx = (float)e.PageSettings.HardMarginX;
            float hy = (float)e.PageSettings.HardMarginY;
            if (hx < 0f || hx > 400f) hx = 0f;
            if (hy < 0f || hy > 400f) hy = 0f;

            float w;
            float h;
            if (mb.Width > 100 && mb.Height > 100)
            {
                w = mb.Width;
                h = mb.Height;
            }
            else
            {
                w = e.PageBounds.Width - 2f * hx;
                h = e.PageBounds.Height - 2f * hy;
            }
            if (w < 100f) w = Math.Max(100f, e.PageBounds.Width);
            if (h < 100f) h = Math.Max(100f, e.PageBounds.Height);
            return new RectangleF(0f, 0f, w, h);
        }

        private static RectangleF FitRect(PrintPageEventArgs e, Image img, PdfPageInfo page, PrintOptions opt)
        {
            RectangleF printable = PrintableArea(e, opt);
            float inset = (float)(Math.Max(0.0, Math.Min(30.0, opt.MarginMm)) / 25.4 * 100.0);
            if (printable.Width - inset * 2f < 80f || printable.Height - inset * 2f < 80f) inset = 0f;
            var area = new RectangleF(printable.X + inset, printable.Y + inset, printable.Width - inset * 2f, printable.Height - inset * 2f);

            if (string.Equals(opt.Scale, "actual", StringComparison.OrdinalIgnoreCase))
            {
                float w = (float)(page.WidthMm / 25.4 * 100.0);
                float h = (float)(page.HeightMm / 25.4 * 100.0);
                return new RectangleF(area.X + (area.Width - w) / 2f, area.Y + (area.Height - h) / 2f, w, h);
            }

            float ratio = Math.Min(area.Width / img.Width, area.Height / img.Height);
            float dw = img.Width * ratio;
            float dh = img.Height * ratio;
            return new RectangleF(area.X + (area.Width - dw) / 2f, area.Y + (area.Height - dh) / 2f, dw, dh);
        }

        private static string PaperLabel(string mode, PdfPageInfo page)
        {
            string p = (mode ?? "auto").Trim();
            if (p.Equals("auto", StringComparison.OrdinalIgnoreCase))
                p = Math.Max(page.WidthMm, page.HeightMm) > 310.0 ? "A3" : "A4";
            return p.ToUpperInvariant();
        }

        private static bool ResolveLandscape(string mode, PdfPageInfo page)
        {
            string m = (mode ?? "auto").Trim();
            if (m.Equals("landscape", StringComparison.OrdinalIgnoreCase)) return true;
            if (m.Equals("portrait", StringComparison.OrdinalIgnoreCase)) return false;
            return page.WidthMm > page.HeightMm;
        }

        private static PaperSize ResolvePaper(string mode, PdfPageInfo page)
        {
            string p = (mode ?? "auto").Trim();
            if (p.Equals("auto", StringComparison.OrdinalIgnoreCase))
                p = Math.Max(page.WidthMm, page.HeightMm) > 310.0 ? "A3" : "A4";
            switch (p.ToUpperInvariant())
            {
                case "A3": return MakePaper(PaperKind.A3, "A3", 297.0, 420.0);
                case "A5": return MakePaper(PaperKind.A5, "A5", 148.0, 210.0);
                case "LETTER": return MakePaper(PaperKind.Letter, "Letter", 215.9, 279.4);
                default: return MakePaper(PaperKind.A4, "A4", 210.0, 297.0);
            }
        }

        private static PaperSize MakePaper(PaperKind kind, string name, double widthMm, double heightMm)
        {
            var ps = new PaperSize(name, (int)Math.Round(widthMm / 25.4 * 100.0), (int)Math.Round(heightMm / 25.4 * 100.0));
            ps.RawKind = (int)kind;
            return ps;
        }

        private void PrintViaWps(string file, PrintOptions opt)
        {
            string previous = new PrinterSettings().PrinterName;
            bool switched = !string.IsNullOrEmpty(opt.Printer) && !string.Equals(previous, opt.Printer, StringComparison.OrdinalIgnoreCase);
            if (switched) SetDefaultPrinter(opt.Printer);
            try
            {
                var psi = new ProcessStartInfo(file)
                {
                    Verb = "print",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(180000);
                }
                Thread.Sleep(1500);
            }
            finally
            {
                if (switched && !string.IsNullOrEmpty(previous)) SetDefaultPrinter(previous);
            }
        }
    }

    internal static class Cli
    {
        public static int Run(string[] args)
        {
            var opt = new PrintOptions { Engine = "render", Paper = "auto", Scale = "fit", Dpi = 300, Duplex = Duplex.Simplex };
            var files = new List<string>();
            string settingsPython = "";
            string logFile = "";
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i];
                string value = i + 1 < args.Length ? args[i + 1] : "";
                switch (key)
                {
                    case "--files": files.AddRange(value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())); i++; break;
                    case "--folder":
                        if (Directory.Exists(value)) files.AddRange(PdfEngine.CollectFiles(value));
                        i++; break;
                    case "--printer": opt.Printer = value; i++; break;
                    case "--engine": opt.Engine = value; i++; break;
                    case "--copies": { int c; int.TryParse(value, out c); opt.Copies = Math.Max(1, c); i++; break; }
                    case "--duplex":
                        opt.Duplex = value.Equals("horizontal", StringComparison.OrdinalIgnoreCase) ? Duplex.Horizontal
                                   : value.Equals("vertical", StringComparison.OrdinalIgnoreCase) ? Duplex.Vertical
                                   : Duplex.Simplex;
                        i++; break;
                    case "--paper": opt.Paper = value; i++; break;
                    case "--orient": opt.Orientation = value; i++; break;
                    case "--scale": opt.Scale = value; i++; break;
                    case "--dpi": { int d; int.TryParse(value, out d); opt.Dpi = d > 0 ? d : 300; i++; break; }
                    case "--margin": { double m; double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out m); opt.MarginMm = Math.Max(0.0, Math.Min(30.0, m)); i++; break; }
                    case "--merge": opt.Merge = value; i++; break;
                    case "--crop-invoice": opt.CropInvoice = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); i++; break;
                    case "--save-merged": opt.SaveMerged = value; i++; break;
                    case "--gray": opt.Gray = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); i++; break;
                    case "--print-to-file": opt.PrintToFile = true; opt.PrintFileName = value; i++; break;
                    case "--python": settingsPython = value; i++; break;
                    case "--log": logFile = value; i++; break;
                }
            }

            var sb = new StringBuilder();
            Action<string> log = delegate(string m)
            {
                string line = DateTime.Now.ToString("HH:mm:ss") + "  " + m;
                sb.AppendLine(line);
                Console.WriteLine(line);
            };

            if (files.Count == 0) { Console.Error.WriteLine("未指定文件 (--files 或 --folder)"); return 2; }
            if (string.IsNullOrEmpty(opt.Printer)) { Console.Error.WriteLine("未指定打印机 (--printer)"); return 2; }
            if (opt.PrintToFile && files.Count > 1 && !BatchPrinter.MergeActive(opt)) { Console.Error.WriteLine("--print-to-file 只支持单个文件（或用 --merge 先合并）"); return 2; }

            opt.Python = PdfEngine.ResolvePython(settingsPython);
            if (opt.Python == null && opt.Engine != "wps")
            {
                Console.Error.WriteLine("未找到可用的 Python + PyMuPDF");
                return 3;
            }
            log("打印机: " + opt.Printer + " | 引擎: " + opt.Engine + " | 文件数: " + files.Count
                + (BatchPrinter.MergeActive(opt) ? " | 合并: " + opt.Merge : ""));

            var printer = new BatchPrinter();
            printer.Log += log;
            printer.Run(files, opt);

            if (!string.IsNullOrEmpty(logFile)) { try { File.WriteAllText(logFile, sb.ToString(), Encoding.UTF8); } catch { } }
            return printer.FailedCount > 0 ? 1 : 0;
        }

        public static void Usage()
        {
            Console.WriteLine("PDF 批量打印工具 (命令行模式)");
            Console.WriteLine("用法: PdfBatchPrint.exe --cli --folder <目录> --printer <打印机名> [选项]");
            Console.WriteLine("      PdfBatchPrint.exe --cli --files \"a.pdf;b.pdf\" --printer <打印机名> [选项]");
            Console.WriteLine("");
            Console.WriteLine("  --files <列表>     分号分隔的 PDF / 图片路径");
            Console.WriteLine("  --folder <目录>    递归收集目录下所有 PDF 和图片");
            Console.WriteLine("                     图片支持 png/jpg/jpeg/bmp/gif/tif/tiff/webp；页尺寸按图片自带 dpi 换算");
            Console.WriteLine("  --printer <名称>   打印机名称 (默认取系统默认打印机，必须指定)");
            Console.WriteLine("  --engine <模式>    render=精确渲染(默认) / wps=调用 WPS 原样打印");
            Console.WriteLine("  --paper <规格>     auto(默认)/A4/A3/A5/Letter");
            Console.WriteLine("  --orient <方向>    auto(默认，按图纸自动横竖)/landscape/portrait");
            Console.WriteLine("  --scale <模式>     fit=适合纸张(默认) / actual=实际大小");
            Console.WriteLine("  --dpi <数值>       渲染分辨率，默认 300");
            Console.WriteLine("  --margin <毫米>    安全边距(距可打印区)，默认 1；边缘被裁就调大，如 3");
            Console.WriteLine("  --copies <份数>    默认 1");
            Console.WriteLine("  --duplex <模式>    simplex(默认)/horizontal/vertical");
            Console.WriteLine("  --merge <模式>     off(默认)/1up/2up/4up  合并发票：每页 1/2/4 张");
            Console.WriteLine("  --crop-invoice <0|1> 合并时裁掉发票白边，默认 0");
            Console.WriteLine("  --save-merged <路径> 把合并结果另存一份 PDF");
            Console.WriteLine("  --gray <0|1>       灰度打印，默认 0");
            Console.WriteLine("  --print-to-file <路径>  打印到文件(仅单文件，测试用)");
            Console.WriteLine("  --python <路径>    指定 Python 解释器(需含 PyMuPDF)");
            Console.WriteLine("  --log <路径>       把日志写入文件");
        }
    }

    public sealed class MainForm : Form
    {
        private readonly ListView lv = new ListView();
        private readonly ComboBox cmbPrinter = new ComboBox();
        private readonly ComboBox cmbEngine = new ComboBox();
        private readonly NumericUpDown numCopies = new NumericUpDown();
        private readonly ComboBox cmbDuplex = new ComboBox();
        private readonly ComboBox cmbPaper = new ComboBox();
        private readonly ComboBox cmbOrient = new ComboBox();
        private readonly ComboBox cmbMargin = new ComboBox();
        private readonly ComboBox cmbScale = new ComboBox();
        private readonly ComboBox cmbDpi = new ComboBox();
        private readonly ComboBox cmbMerge = new ComboBox();
        private readonly CheckBox chkCropInvoice = new CheckBox();
        private readonly Button btnSaveMerged = new Button();
        private readonly CheckBox chkGray = new CheckBox();
        private readonly Button btnAddFiles = new Button();
        private readonly Button btnAddFolder = new Button();
        private readonly Button btnRemove = new Button();
        private readonly Button btnClear = new Button();
        private readonly Button btnStart = new Button();
        private readonly Button btnStop = new Button();
        private readonly Button btnPreview = new Button();
        private readonly ProgressBar bar = new ProgressBar();
        private readonly Label lblStatus = new Label();
        private readonly TextBox txtLog = new TextBox();
        private readonly Settings settings = Settings.Load();
        private Thread worker;
        private BatchPrinter runner;
        private string pythonExe;

        private readonly string[] startFiles;

        public MainForm() : this(new string[0]) { }

        public MainForm(string[] files)
        {
            startFiles = files ?? new string[0];
            Text = "PDF 批量打印工具";
            StartPosition = FormStartPosition.CenterScreen;
            Width = settings.FormWidth >= 1120 ? settings.FormWidth : 1220;
            Height = settings.FormHeight >= 700 ? settings.FormHeight : 800;
            MinimumSize = new Size(1120, 700);
            Font = new Font("Microsoft YaHei UI", 9f);
            AllowDrop = true;
            BuildUi();
            LoadPrinters();
            ApplySettings();
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosing += OnFormClosing;
            Shown += OnShown;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 68f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32f));

            // ---------------- 文件操作 ----------------
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(10, 10, 10, 4) };
            TuneButton(btnAddFiles, "添加文件", 96);
            TuneButton(btnAddFolder, "添加文件夹", 110);
            TuneButton(btnRemove, "移除选中", 96);
            TuneButton(btnClear, "清空列表", 96);
            btnAddFiles.Click += delegate { AddFiles(); };
            btnAddFolder.Click += delegate { AddFolder(); };
            btnRemove.Click += delegate { RemoveChecked(); };
            btnClear.Click += delegate { lv.Items.Clear(); };
            top.Controls.Add(btnAddFiles);
            top.Controls.Add(btnAddFolder);
            top.Controls.Add(btnRemove);
            top.Controls.Add(btnClear);

            // ---------------- 文件列表 ----------------
            lv.Dock = DockStyle.Fill;
            lv.Margin = new Padding(10, 0, 10, 8);
            lv.View = View.Details;
            lv.CheckBoxes = true;
            lv.FullRowSelect = true;
            lv.GridLines = true;
            lv.HideSelection = false;
            lv.ShowItemToolTips = true;
            lv.Columns.Add("文件", 320);
            lv.Columns.Add("页数", 56, HorizontalAlignment.Right);
            lv.Columns.Add("页面尺寸", 150);
            lv.Columns.Add("路径", 380);
            lv.Resize += delegate
            {
                if (lv.Columns.Count < 4) return;
                int used = 320 + 56 + 150;
                lv.Columns[0].Width = Math.Max(180, (lv.ClientSize.Width - used) * 45 / 100);
                lv.Columns[3].Width = Math.Max(120, lv.ClientSize.Width - used - lv.Columns[0].Width - 8);
            };
            lv.DoubleClick += delegate
            {
                if (lv.SelectedItems.Count == 0) return;
                try { Process.Start(lv.SelectedItems[0].Tag as string); } catch { }
            };

            // ---------------- 打印设置 ----------------
            var grp = new GroupBox { Text = "打印设置", Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 6), Padding = new Padding(10, 4, 10, 4) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            rows.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));
            rows.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            rows.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            var rowA = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            var rowB = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };

            cmbPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPrinter.DropDownWidth = 520;
            cmbPrinter.IntegralHeight = false;
            cmbPrinter.DropDownHeight = 320;
            AddField(rowA, "打印机", cmbPrinter, 300);
            numCopies.Minimum = 1; numCopies.Maximum = 99; numCopies.Value = 1;
            AddField(rowA, "份数", numCopies, 62);
            cmbDuplex.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDuplex.Items.AddRange(new object[] { "单面", "双面（长边翻转）", "双面（短边翻转）" });
            AddField(rowA, "双面", cmbDuplex, 168);
            cmbEngine.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbEngine.Items.AddRange(new object[] { "精确渲染（推荐）", "WPS 原样打印" });
            AddField(rowA, "打印引擎", cmbEngine, 168);

            cmbPaper.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPaper.Items.AddRange(new object[] { "自动（按原图）", "A4", "A3", "A5", "Letter" });
            AddField(rowB, "纸张", cmbPaper, 112);
            cmbOrient.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbOrient.Items.AddRange(new object[] { "自动（按图纸）", "横向", "纵向" });
            AddField(rowB, "方向", cmbOrient, 112);
            cmbMargin.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMargin.Items.AddRange(new object[] { "0 mm（贴边）", "1 mm（默认）", "2 mm", "3 mm", "5 mm" });
            AddField(rowB, "边距", cmbMargin, 112);
            cmbScale.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbScale.Items.AddRange(new object[] { "适合纸张", "实际大小（100%）" });
            AddField(rowB, "缩放", cmbScale, 132);
            cmbDpi.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbDpi.Items.AddRange(new object[] { "150", "200", "300", "400", "600" });
            AddField(rowB, "分辨率", cmbDpi, 76);
            chkGray.Text = "灰度打印";
            chkGray.Width = 100;
            chkGray.Height = 26;
            chkGray.Margin = new Padding(6, 8, 12, 0);
            rowB.Controls.Add(chkGray);
            var lblTip = new Label
            {
                Text = "建议 300dpi + 适合纸张",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(10, 12, 0, 0)
            };
            rowB.Controls.Add(lblTip);

            var rowC = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            cmbMerge.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbMerge.Items.AddRange(new object[] { "不合并（原样打印）", "合并：每页 1 张", "合并：每页 2 张（发票推荐）", "合并：每页 4 张" });
            AddField(rowC, "合并", cmbMerge, 214);
            chkCropInvoice.Text = "裁掉白边";
            chkCropInvoice.Width = 104;
            chkCropInvoice.Height = 26;
            chkCropInvoice.Margin = new Padding(8, 8, 12, 0);
            rowC.Controls.Add(chkCropInvoice);
            TuneButton(btnSaveMerged, "合并另存...", 112);
            btnSaveMerged.Margin = new Padding(0, 2, 8, 0);
            btnSaveMerged.Click += delegate { SaveMergedFile(); };
            rowC.Controls.Add(btnSaveMerged);
            var lblTip2 = new Label
            {
                Text = "发票建议：合并 2 张/页 + 裁掉白边",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(10, 12, 0, 0)
            };
            rowC.Controls.Add(lblTip2);

            rows.Controls.Add(rowA, 0, 0);
            rows.Controls.Add(rowB, 0, 1);
            rows.Controls.Add(rowC, 0, 2);
            grp.Controls.Add(rows);

            // ---------------- 操作区 ----------------
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Margin = new Padding(10, 0, 10, 8) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190f));
            TuneButton(btnStart, "开始打印", 130);
            btnStart.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
            btnStart.BackColor = Color.FromArgb(0, 120, 215);
            btnStart.ForeColor = Color.White;
            btnStart.Dock = DockStyle.Fill;
            btnStart.Margin = new Padding(0, 4, 8, 4);
            TuneButton(btnStop, "停止", 84);
            btnStop.Enabled = false;
            btnStop.Dock = DockStyle.Fill;
            btnStop.Margin = new Padding(0, 4, 8, 4);
            TuneButton(btnPreview, "打印预览", 124);
            btnPreview.Dock = DockStyle.Fill;
            btnPreview.Margin = new Padding(0, 4, 8, 4);
            btnStart.Click += delegate { StartPrint(); };
            btnStop.Click += delegate { if (runner != null) runner.Cancelled = true; };
            btnPreview.Click += delegate { PreviewClick(); };
            bar.Dock = DockStyle.Fill;
            bar.Margin = new Padding(14, 14, 14, 12);
            lblStatus.Text = "就绪";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.AutoSize = false;
            actions.Controls.Add(btnStart, 0, 0);
            actions.Controls.Add(btnStop, 1, 0);
            actions.Controls.Add(btnPreview, 2, 0);
            actions.Controls.Add(bar, 3, 0);
            actions.Controls.Add(lblStatus, 4, 0);

            // ---------------- 日志 ----------------
            txtLog.Dock = DockStyle.Fill;
            txtLog.Margin = new Padding(10, 0, 10, 10);
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.BackColor = Color.White;
            txtLog.Font = new Font("Consolas", 9f);

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(lv, 0, 1);
            root.Controls.Add(grp, 0, 2);
            root.Controls.Add(actions, 0, 3);
            root.Controls.Add(txtLog, 0, 4);
            Controls.Add(root);
        }

        private static void TuneButton(Button b, string text, int width)
        {
            b.Text = text;
            b.Width = width;
            b.Height = 32;
            b.FlatStyle = FlatStyle.System;
            b.Margin = new Padding(0, 2, 8, 0);
            b.UseVisualStyleBackColor = true;
        }

        private static void AddField(FlowLayoutPanel row, string label, Control ctl, int width)
        {
            row.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(row.Controls.Count == 0 ? 0 : 14, 10, 6, 0)
            });
            ctl.Width = width;
            ctl.Margin = new Padding(0, 6, 0, 0);
            if (ctl is ComboBox) ((ComboBox)ctl).Height = 26;
            row.Controls.Add(ctl);
        }

        private sealed class PrinterEntry
        {
            public readonly string Name;
            public readonly string Port;
            public readonly bool Placeholder;
            public readonly bool Dead;

            public PrinterEntry(string name, string port, bool placeholder, bool dead)
            {
                Name = name;
                Port = port;
                Placeholder = placeholder;
                Dead = dead;
            }

            public override string ToString()
            {
                if (Placeholder) return Name + "   [本机占位端口，打不出纸]";
                if (Dead) return Name + "   [端口 " + Port + "，没有接打印机]";
                return Name;
            }
        }

        private static Dictionary<string, string> QueryPrinterPorts()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var q = new System.Management.ManagementObjectSearcher("SELECT Name, PortName FROM Win32_Printer"))
                {
                    foreach (System.Management.ManagementBaseObject mo in q.Get())
                    {
                        string name = Convert.ToString(mo["Name"]);
                        string port = Convert.ToString(mo["PortName"]);
                        if (!string.IsNullOrEmpty(name)) map[name] = port ?? "";
                    }
                }
            }
            catch { }
            return map;
        }

        private void LoadPrinters()
        {
            cmbPrinter.Items.Clear();
            var ports = QueryPrinterPorts();
            var installed = new List<string>();
            try
            {
                foreach (string p in PrinterSettings.InstalledPrinters) installed.Add(p);
                installed.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                AppendLog("枚举打印机失败: " + ex.Message);
            }

            var entries = new List<PrinterEntry>();
            foreach (string name in installed)
            {
                string port;
                if (!ports.TryGetValue(name, out port)) port = "";
                bool prompt = string.Equals(port, "PORTPROMPT:", StringComparison.OrdinalIgnoreCase);
                bool virtualOk = name.IndexOf("Print to PDF", StringComparison.OrdinalIgnoreCase) >= 0
                                 || name.IndexOf("XPS", StringComparison.OrdinalIgnoreCase) >= 0
                                 || name.IndexOf("OneNote", StringComparison.OrdinalIgnoreCase) >= 0;
                bool dead = !virtualOk && DeadPort(port);
                entries.Add(new PrinterEntry(name, port, prompt && !virtualOk, dead));
            }
            entries.Sort(delegate (PrinterEntry a, PrinterEntry b)
            {
                if (a.Placeholder != b.Placeholder) return a.Placeholder ? 1 : -1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            foreach (PrinterEntry e in entries) cmbPrinter.Items.Add(e);
            AppendLog("检测到 " + cmbPrinter.Items.Count + " 台可用打印机。");

            string systemDefault = new PrinterSettings().PrinterName;
            PrinterEntry choice = PickPrinter(settings.Printer, systemDefault);
            cmbPrinter.SelectedItem = choice;

            if (choice != null && choice.Placeholder)
                AppendLog("注意：当前打印机端口是 PORTPROMPT:（本机占位），任务不会真正输出。请改选带 \\\\服务器\\ 前缀的那台。");
        }

        private static bool DeadPort(string port)
        {
            string p = (port ?? "").Trim().ToUpperInvariant();
            if (p.Length == 0) return false;
            if (p == "FILE:" || p == "XPSPORT:") return true;
            if (p.StartsWith("COM") || p.StartsWith("LPT")) return true;
            return false;
        }

        private PrinterEntry FindPrinterEntry(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (object o in cmbPrinter.Items)
            {
                PrinterEntry e = o as PrinterEntry;
                if (e != null && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) return e;
            }
            return null;
        }

        private PrinterEntry PickPrinter(string saved, string systemDefault)
        {
            PrinterEntry e = FindPrinterEntry(saved);
            if (e != null && !e.Placeholder && !e.Dead) return e;
            if (e != null && e.Placeholder)
            {
                PrinterEntry share = PreferShare(e);
                if (share != null) return share;
            }
            if (e != null && e.Dead)
                AppendLog("上次使用的 " + e.Name + " 端口是 " + e.Port + "，没有接打印机，已自动改选。");

            PrinterEntry f = FindPrinterEntry(systemDefault);
            if (f != null && !f.Placeholder && !f.Dead) return f;
            if (f != null && f.Placeholder)
            {
                PrinterEntry share = PreferShare(f);
                if (share != null) return share;
            }

            foreach (object o in cmbPrinter.Items)
            {
                PrinterEntry x = o as PrinterEntry;
                if (x != null && !x.Placeholder && !x.Dead) return x;
            }
            if (e != null) return e;
            if (f != null) return f;
            return cmbPrinter.Items.Count > 0 ? cmbPrinter.Items[0] as PrinterEntry : null;
        }

        private PrinterEntry PreferShare(PrinterEntry placeholder)
        {
            foreach (object o in cmbPrinter.Items)
            {
                PrinterEntry e = o as PrinterEntry;
                if (e == null || e.Placeholder || !e.Name.StartsWith("\\\\")) continue;
                if (e.Name.EndsWith("\\" + placeholder.Name, StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog("本机的 " + placeholder.Name + " 是占位端口，已自动改用共享打印机 " + e.Name + "。");
                    return e;
                }
            }
            return null;
        }

        private void ApplySettings()
        {
            cmbEngine.SelectedIndex = string.Equals(settings.Engine, "wps", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            numCopies.Value = Math.Max(1, Math.Min(99, settings.Copies));
            cmbDuplex.SelectedIndex = settings.Duplex == "horizontal" ? 1 : settings.Duplex == "vertical" ? 2 : 0;
            switch ((settings.Paper ?? "auto").ToUpperInvariant())
            {
                case "A4": cmbPaper.SelectedIndex = 1; break;
                case "A3": cmbPaper.SelectedIndex = 2; break;
                case "A5": cmbPaper.SelectedIndex = 3; break;
                case "LETTER": cmbPaper.SelectedIndex = 4; break;
                default: cmbPaper.SelectedIndex = 0; break;
            }
            switch ((settings.Orientation ?? "auto").ToLowerInvariant())
            {
                case "landscape": cmbOrient.SelectedIndex = 1; break;
                case "portrait": cmbOrient.SelectedIndex = 2; break;
                default: cmbOrient.SelectedIndex = 0; break;
            }
            cmbScale.SelectedIndex = settings.Scale == "actual" ? 1 : 0;
            string dpiText = settings.Dpi.ToString(CultureInfo.InvariantCulture);
            if (!cmbDpi.Items.Contains(dpiText)) cmbDpi.Items.Add(dpiText);
            cmbDpi.SelectedItem = dpiText;
            if (cmbDpi.SelectedIndex < 0) cmbDpi.SelectedIndex = 2;
            chkGray.Checked = settings.Gray;
            double[] margins = { 0.0, 1.0, 2.0, 3.0, 5.0 };
            int marginIndex = 1;
            for (int i = 0; i < margins.Length; i++)
                if (Math.Abs(margins[i] - settings.MarginMm) < 0.01) marginIndex = i;
            cmbMargin.SelectedIndex = marginIndex;
            cmbMerge.SelectedIndex = string.Equals(settings.Merge, "1up", StringComparison.OrdinalIgnoreCase) ? 1
                : string.Equals(settings.Merge, "2up", StringComparison.OrdinalIgnoreCase) ? 2
                : string.Equals(settings.Merge, "4up", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
            chkCropInvoice.Checked = settings.CropInvoice;
        }

        private void OnShown(object sender, EventArgs e)
        {
            AppendLog("PDF 批量打印工具已启动。双击列表项可用系统默认程序打开。");
            ThreadPool.QueueUserWorkItem(delegate
            {
                string py = PdfEngine.ResolvePython(settings.PythonPath);
                pythonExe = py;
                if (py == null)
                {
                    SafeInvoke(delegate
                    {
                        AppendLog("提示：未检测到 Python + PyMuPDF，精确渲染引擎不可用（可使用 WPS 原样打印）。");
                    });
                }
                else
                {
                    SafeInvoke(delegate { AppendLog("渲染引擎就绪: " + py); });
                }
            });
            if (startFiles.Length > 0)
            {
                string[] copy = startFiles;
                BeginInvoke((MethodInvoker)delegate { AddPaths(copy); });
            }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (runner != null) runner.Cancelled = true;
            PrinterEntry entry = cmbPrinter.SelectedItem as PrinterEntry;
            settings.Printer = entry != null ? entry.Name : "";
            settings.Engine = cmbEngine.SelectedIndex == 1 ? "wps" : "render";
            settings.Copies = (int)numCopies.Value;
            settings.Duplex = cmbDuplex.SelectedIndex == 1 ? "horizontal" : cmbDuplex.SelectedIndex == 2 ? "vertical" : "simplex";
            settings.Paper = new[] { "auto", "A4", "A3", "A5", "Letter" }[Math.Max(0, cmbPaper.SelectedIndex)];
            settings.Orientation = new[] { "auto", "landscape", "portrait" }[Math.Max(0, cmbOrient.SelectedIndex)];
            settings.Scale = cmbScale.SelectedIndex == 1 ? "actual" : "fit";
            int dpi;
            int.TryParse(cmbDpi.SelectedItem as string, out dpi);
            settings.Dpi = dpi > 0 ? dpi : 300;
            settings.Gray = chkGray.Checked;
            settings.MarginMm = new[] { 0.0, 1.0, 2.0, 3.0, 5.0 }[Math.Max(0, cmbMargin.SelectedIndex)];
            settings.Merge = new[] { "off", "1up", "2up", "4up" }[Math.Max(0, cmbMerge.SelectedIndex)];
            settings.CropInvoice = chkCropInvoice.Checked;
            settings.FormWidth = Width;
            settings.FormHeight = Height;
            settings.Save();
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null) AddPaths(paths);
        }

        private void AddFiles()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择要打印的 PDF 或图片";
                dlg.Filter = PdfEngine.SupportedFilter;
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(dlg.FileNames);
            }
        }

        private void AddFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择包含 PDF / 图片的文件夹（含子文件夹）";
                if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dlg.SelectedPath });
            }
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            var files = new List<string>();
            foreach (string p in paths)
            {
                try
                {
                    if (Directory.Exists(p)) files.AddRange(PdfEngine.CollectFiles(p));
                    else if (File.Exists(p) && PdfEngine.IsSupportedPath(p)) files.Add(p);
                }
                catch { }
            }
            var known = new HashSet<string>(lv.Items.Cast<ListViewItem>().Select(x => (string)x.Tag), StringComparer.OrdinalIgnoreCase);
            var added = new List<string>();
            foreach (string f in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (known.Contains(f)) continue;
                known.Add(f);
                added.Add(f);
                var item = new ListViewItem(Path.GetFileName(f));
                item.SubItems.Add("...");
                item.SubItems.Add("...");
                item.SubItems.Add(Path.GetDirectoryName(f));
                item.Tag = f;
                item.Checked = true;
                lv.Items.Add(item);
            }
            if (added.Count == 0) return;
            AppendLog("已添加 " + added.Count + " 个文件，正在读取页面信息...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string py = pythonExe ?? PdfEngine.ResolvePython(settings.PythonPath);
                    if (py == null)
                    {
                        SafeInvoke(delegate { AppendLog("未检测到 Python + PyMuPDF，无法读取页面信息（仍可用 WPS 原样打印）。"); });
                        return;
                    }
                    var infos = PdfEngine.GetInfo(py, added);
                    SafeInvoke(delegate
                    {
                        foreach (var info in infos)
                        {
                            var item = lv.Items.Cast<ListViewItem>().FirstOrDefault(x => string.Equals((string)x.Tag, info.Path, StringComparison.OrdinalIgnoreCase));
                            if (item == null) continue;
                            if (!string.IsNullOrEmpty(info.Error))
                            {
                                item.SubItems[1].Text = "错误";
                                item.SubItems[2].Text = info.Error;
                                item.Checked = false;
                                continue;
                            }
                            item.SubItems[1].Text = info.Pages.Count.ToString();
                            item.SubItems[2].Text = info.Pages.Count > 0 ? info.Pages[0].SizeLabel + (info.Pages.Count > 1 ? " …" : "") : "-";
                        }
                        AppendLog("页面信息读取完成。");
                    });
                }
                catch (Exception ex)
                {
                    SafeInvoke(delegate { AppendLog("读取页面信息失败: " + ex.Message); });
                }
            });
        }

        private void RemoveChecked()
        {
            for (int i = lv.Items.Count - 1; i >= 0; i--)
                if (lv.Items[i].Checked || lv.Items[i].Selected) lv.Items.RemoveAt(i);
        }

        private void StartPrint()
        {
            if (worker != null && worker.IsAlive) return;
            PrinterEntry entry = cmbPrinter.SelectedItem as PrinterEntry;
            if (entry == null) { MessageBox.Show(this, "请先选择打印机。", "提示"); return; }
            string printerName = entry.Name;
            if (entry.Dead)
            {
                DialogResult go2 = MessageBox.Show(this,
                    "当前选择的打印机是：\n" + entry.Name + "\n\n它的端口是 " + entry.Port + "，没有连接打印机，任务不会输出。\n\n仍要继续吗？",
                    "打印机端口不可用", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (go2 != DialogResult.OK) return;
            }
            if (entry.Placeholder)
            {
                DialogResult go = MessageBox.Show(this,
                    "当前选择的打印机是：\n" + entry.Name + "\n\n它的端口是 PORTPROMPT:（本机占位），任务不会真正输出到打印机。\n" +
                    "请改选带 \\\\服务器\\ 前缀的共享打印机（例如 \\\\Server1\\HP MFP M433 PCL 6）。\n\n仍要按当前选择继续吗？",
                    "打印机可能是占位端口", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (go != DialogResult.OK) return;
            }
            var files = lv.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(x => (string)x.Tag).ToList();
            if (files.Count == 0) { MessageBox.Show(this, "请先添加并勾选要打印的 PDF。", "提示"); return; }

            var opt = CollectOptions(entry);

            if (opt.Engine == "wps")
                AppendLog("注意：WPS 原样打印使用 WPS 自身的打印设置，份数/纸张等选项不生效。");
            if (opt.Engine == "render" && string.IsNullOrEmpty(opt.Python))
            {
                MessageBox.Show(this, "未找到 Python + PyMuPDF，无法使用精确渲染。请改用 WPS 原样打印，或安装 Python 后设置 PDFBATCH_PYTHON 环境变量。", "缺少渲染引擎");
                return;
            }

            string mergeText = opt.Merge == "off" ? "不合并"
                : "合并" + (opt.Merge == "1up" ? "每页1张" : opt.Merge == "4up" ? "每页4张" : "每页2张")
                  + (opt.CropInvoice ? "（裁掉白边）" : "");
            var dialog = MessageBox.Show(this,
                string.Format("将打印 {0} 个文件到：\n{1}\n\n份数 {2}，{3}，纸张 {4}，方向 {5}，边距 {8}，{6}，{7}dpi，{9}。\n\n开始打印吗？",
                    files.Count, printerName, opt.Copies,
                    cmbDuplex.Text, cmbPaper.Text, cmbOrient.Text, cmbScale.Text, opt.Dpi,
                    opt.MarginMm.ToString("0.#", CultureInfo.InvariantCulture), mergeText),
                "确认打印", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (dialog != DialogResult.OK) return;

            txtLog.Clear();
            bar.Value = 0;
            bar.Maximum = Math.Max(1, files.Count);
            btnStart.Enabled = false;
            btnPreview.Enabled = false;
            btnStop.Enabled = true;
            AppendLog("========== 开始批量打印 ==========");

            runner = new BatchPrinter();
            runner.Log += m => SafeInvoke(delegate { AppendLog(m); });
            runner.Progress += (done, total, msg) => SafeInvoke(delegate
            {
                bar.Maximum = Math.Max(1, total);
                bar.Value = Math.Min(bar.Maximum, done);
                lblStatus.Text = msg;
                if (done >= total && total > 0)
                {
                    btnStart.Enabled = true;
                    btnPreview.Enabled = true;
                    btnStop.Enabled = false;
                }
            });
            var captured = runner;
            worker = new Thread(delegate ()
            {
                try { captured.Run(files, opt); }
                catch (Exception ex) { SafeInvoke(delegate { AppendLog("异常: " + ex.Message); }); }
                finally
                {
                    SafeInvoke(delegate
                    {
                        btnStart.Enabled = true;
                        btnPreview.Enabled = true;
                        btnStop.Enabled = false;
                        lblStatus.Text = "就绪";
                    });
                }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private PrintOptions CollectOptions(PrinterEntry entry)
        {
            var opt = new PrintOptions
            {
                Printer = entry != null ? entry.Name : "",
                Engine = cmbEngine.SelectedIndex == 1 ? "wps" : "render",
                Copies = (int)numCopies.Value,
                Duplex = cmbDuplex.SelectedIndex == 1 ? Duplex.Horizontal : cmbDuplex.SelectedIndex == 2 ? Duplex.Vertical : Duplex.Simplex,
                Paper = new[] { "auto", "A4", "A3", "A5", "Letter" }[Math.Max(0, cmbPaper.SelectedIndex)],
                Orientation = new[] { "auto", "landscape", "portrait" }[Math.Max(0, cmbOrient.SelectedIndex)],
                Scale = cmbScale.SelectedIndex == 1 ? "actual" : "fit",
                MarginMm = new[] { 0.0, 1.0, 2.0, 3.0, 5.0 }[Math.Max(0, cmbMargin.SelectedIndex)],
                Merge = new[] { "off", "1up", "2up", "4up" }[Math.Max(0, cmbMerge.SelectedIndex)],
                CropInvoice = chkCropInvoice.Checked,
                Gray = chkGray.Checked,
                Python = pythonExe ?? PdfEngine.ResolvePython(settings.PythonPath)
            };
            int dpi;
            int.TryParse(cmbDpi.SelectedItem as string, out dpi);
            opt.Dpi = dpi > 0 ? dpi : 300;
            return opt;
        }

        private void PreviewClick()
        {
            if (worker != null && worker.IsAlive) return;
            PrinterEntry entry = cmbPrinter.SelectedItem as PrinterEntry;
            var files = lv.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(x => (string)x.Tag).ToList();
            if (files.Count == 0) { MessageBox.Show(this, "请先添加并勾选要预览的 PDF。", "提示"); return; }
            PrintOptions opt = CollectOptions(entry);
            if (string.IsNullOrEmpty(opt.Python))
            {
                MessageBox.Show(this, "打印预览需要 Python + PyMuPDF（渲染引擎）。\n请安装 Python + PyMuPDF，或设置 PDFBATCH_PYTHON 环境变量后重试。", "缺少渲染引擎");
                return;
            }
            btnPreview.Enabled = false;
            AppendLog("正在生成打印预览（约 " + files.Count + " 个文件）...");
            StartPreview(files, opt);
        }

        private void StartPreview(List<string> files, PrintOptions opt)
        {
            var th = new Thread(delegate ()
            {
                string tmp = null;
                try
                {
                    tmp = Path.Combine(PreviewTempRoot(), Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tmp);
                    List<PreviewPage> pages = BuildPreviewPages(files, opt, tmp);
                    string folder = tmp;
                    SafeInvoke(delegate
                    {
                        btnPreview.Enabled = true;
                        if (pages.Count == 0)
                        {
                            AppendLog("预览失败：没有生成任何页面图，请检查文件是否正常。");
                            MessageBox.Show(this, "没能生成预览页面，请检查日志。", "打印预览");
                            try { Directory.Delete(folder, true); } catch { }
                            return;
                        }
                        AppendLog("预览就绪：" + pages.Count + " 页。");
                        try
                        {
                            using (var form = new PreviewForm(pages, opt)) form.ShowDialog(this);
                        }
                        catch (Exception ex)
                        {
                            AppendLog("预览窗口出错: " + ex.Message);
                        }
                        finally
                        {
                            try { Directory.Delete(folder, true); } catch { }
                        }
                    });
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                    SafeInvoke(delegate
                    {
                        btnPreview.Enabled = true;
                        AppendLog("预览失败: " + msg);
                    });
                    try { if (tmp != null) Directory.Delete(tmp, true); } catch { }
                }
            });
            th.IsBackground = true;
            th.Start();
        }

        private List<PreviewPage> BuildPreviewPages(List<string> files, PrintOptions opt, string tmp)
        {
            var result = new List<PreviewPage>();
            const int maxPages = 80;
            if (BatchPrinter.MergeActive(opt))
            {
                string merged = PdfEngine.Merge(opt.Python, files, Path.Combine(tmp, "merged.pdf"),
                    BatchPrinter.NupValue(opt.Merge), opt.CropInvoice, BatchPrinter.MergePaper(opt.Paper));
                foreach (PdfFileInfo p in PdfEngine.GetInfo(opt.Python, new[] { merged }))
                {
                    if (!p.Ok || p.Pages.Count == 0) continue;
                    string dir = Path.Combine(tmp, "merged");
                    Directory.CreateDirectory(dir);
                    foreach (string img in PdfEngine.Render(opt.Python, merged, dir, 110, opt.Gray))
                    {
                        if (result.Count >= maxPages) break;
                        int idx = result.Count;
                        var page = p.Pages[Math.Min(idx, p.Pages.Count - 1)];
                        string label = "合并结果（" + files.Count + " 个文件）· 第 " + (idx + 1) + " 页";
                        result.Add(new PreviewPage { ImagePath = img, Page = page, Label = label });
                    }
                }
                return result;
            }
            int fileNo = 0;
            foreach (string f in files)
            {
                if (result.Count >= maxPages) break;
                fileNo++;
                List<PdfFileInfo> infoList = PdfEngine.GetInfo(opt.Python, new[] { f });
                if (infoList.Count == 0 || !infoList[0].Ok) continue;
                PdfFileInfo info = infoList[0];
                string dir = Path.Combine(tmp, "f" + fileNo);
                Directory.CreateDirectory(dir);
                List<string> images = PdfEngine.Render(opt.Python, f, dir, 110, opt.Gray);
                for (int i = 0; i < images.Count && i < info.Pages.Count && result.Count < maxPages; i++)
                {
                    result.Add(new PreviewPage
                    {
                        ImagePath = images[i],
                        Page = info.Pages[i],
                        Label = Path.GetFileName(f) + " · 第 " + (i + 1) + " 页"
                    });
                }
            }
            return result;
        }

        private static string PreviewTempRoot()
        {
            string[] candidates =
            {
                Path.Combine(Path.GetTempPath(), "PdfBatchPrint"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfBatchPrint", "temp"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp")
            };
            foreach (string c in candidates)
            {
                try
                {
                    Directory.CreateDirectory(c);
                    string probe = Path.Combine(c, "probe_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(probe);
                    Directory.Delete(probe, true);
                    return c;
                }
                catch { }
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
        }

        private void SaveMergedFile()
        {
            var files = lv.Items.Cast<ListViewItem>().Where(x => x.Checked).Select(x => (string)x.Tag).ToList();
            if (files.Count == 0) { MessageBox.Show(this, "请先添加并勾选要合并的 PDF。", "提示"); return; }
            string merge = new[] { "off", "1up", "2up", "4up" }[Math.Max(0, cmbMerge.SelectedIndex)];
            if (merge == "off") merge = "2up";
            string py = pythonExe ?? PdfEngine.ResolvePython(settings.PythonPath);
            if (string.IsNullOrEmpty(py))
            {
                MessageBox.Show(this, "合并需要 Python + PyMuPDF，请先安装或设置 PDFBATCH_PYTHON 环境变量。", "缺少渲染引擎");
                return;
            }
            string paper = new[] { "auto", "A4", "A3", "A5", "Letter" }[Math.Max(0, cmbPaper.SelectedIndex)];
            paper = string.Equals(paper, "A3", StringComparison.OrdinalIgnoreCase) ? "A3" : "A4";
            string nup = merge == "1up" ? "1" : merge == "4up" ? "4" : "2";
            bool crop = chkCropInvoice.Checked;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "另存合并后的 PDF";
                dlg.Filter = "PDF 文件 (*.pdf)|*.pdf";
                dlg.FileName = "合并发票_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".pdf";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string outPath = dlg.FileName;
                AppendLog("正在合并 " + files.Count + " 个文件 → " + paper + "（每页 " + nup + " 张）...");
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        string result = PdfEngine.Merge(py, files, outPath, nup, crop, paper);
                        SafeInvoke(delegate { AppendLog("已生成: " + result); });
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(delegate { AppendLog("合并失败: " + ex.Message); });
                    }
                });
            }
        }

        private void AppendLog(string message)
        {
            if (InvokeRequired) { Invoke(new Action<string>(AppendLog), message); return; }
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }

        private void SafeInvoke(MethodInvoker action)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }
    }

    internal sealed class PreviewPage
    {
        public string ImagePath = "";
        public PdfPageInfo Page;
        public string Label = "";
    }

    internal sealed class PreviewForm : Form
    {
        private readonly List<PreviewPage> pages;
        private readonly PrintOptions opt;
        private readonly Dictionary<int, Image> cache = new Dictionary<int, Image>();
        private readonly Label lblCaption = new Label();
        private readonly Label lblPageNo = new Label();
        private readonly Button btnPrev = new Button();
        private readonly Button btnNext = new Button();
        private readonly Button btnZoomIn = new Button();
        private readonly Button btnZoomOut = new Button();
        private readonly Button btnFit = new Button();
        private readonly Button btnClose = new Button();
        private readonly float hardMmX;
        private readonly float hardMmY;
        private int index;
        private float zoom = 1f;

        public PreviewForm(List<PreviewPage> pages, PrintOptions opt)
        {
            this.pages = pages;
            this.opt = opt;

            float hx = 0f, hy = 0f;
            try
            {
                var ps = new PrinterSettings();
                if (!string.IsNullOrEmpty(opt.Printer)) ps.PrinterName = opt.Printer;
                if (ps.IsValid)
                {
                    PageSettings page = ps.DefaultPageSettings;
                    hx = (float)(page.HardMarginX / 100.0 * 25.4);
                    hy = (float)(page.HardMarginY / 100.0 * 25.4);
                }
            }
            catch { }
            if (hx <= 0.1f || hx > 30f) hx = 4.2f;
            if (hy <= 0.1f || hy > 30f) hy = 4.2f;
            hardMmX = hx;
            hardMmY = hy;

            Text = "打印预览";
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Width = 1060;
            Height = 800;
            MinimumSize = new Size(780, 520);
            BackColor = Color.FromArgb(56, 56, 56);
            Font = new Font("Microsoft YaHei UI", 9f);
            KeyPreview = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            lblCaption.Dock = DockStyle.Top;
            lblCaption.Height = 38;
            lblCaption.ForeColor = Color.Gainsboro;
            lblCaption.TextAlign = ContentAlignment.MiddleLeft;
            lblCaption.Padding = new Padding(14, 0, 12, 0);
            lblCaption.BackColor = Color.FromArgb(45, 45, 48);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.FromArgb(45, 45, 48) };
            lblPageNo.AutoSize = false;
            lblPageNo.Dock = DockStyle.Left;
            lblPageNo.Width = 210;
            lblPageNo.TextAlign = ContentAlignment.MiddleLeft;
            lblPageNo.Padding = new Padding(14, 0, 0, 0);
            lblPageNo.ForeColor = Color.Gainsboro;

            var right = new FlowLayoutPanel();
            right.Dock = DockStyle.Right;
            right.Width = 620;
            right.FlowDirection = FlowDirection.RightToLeft;
            right.WrapContents = false;
            right.Padding = new Padding(0, 11, 10, 0);

            TunePreviewButton(btnClose, "关闭  Esc", 92);
            TunePreviewButton(btnFit, "适应窗口", 92);
            TunePreviewButton(btnZoomIn, "放大 +", 84);
            TunePreviewButton(btnZoomOut, "缩小 -", 84);
            TunePreviewButton(btnNext, "下一页 ›", 100);
            TunePreviewButton(btnPrev, "‹ 上一页", 100);
            btnClose.Click += delegate { Close(); };
            btnPrev.Click += delegate { GoPage(-1); };
            btnNext.Click += delegate { GoPage(1); };
            btnZoomIn.Click += delegate { SetZoom(zoom + 0.15f); };
            btnZoomOut.Click += delegate { SetZoom(zoom - 0.15f); };
            btnFit.Click += delegate { SetZoom(1f); };

            right.Controls.Add(btnClose);
            right.Controls.Add(btnFit);
            right.Controls.Add(btnZoomIn);
            right.Controls.Add(btnZoomOut);
            right.Controls.Add(btnNext);
            right.Controls.Add(btnPrev);
            bottom.Controls.Add(right);
            bottom.Controls.Add(lblPageNo);

            Controls.Add(bottom);
            Controls.Add(lblCaption);

            UpdateCaption();
        }

        private static void TunePreviewButton(Button b, string text, int width)
        {
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.FlatStyle = FlatStyle.System;
            b.Margin = new Padding(4, 0, 4, 0);
        }

        private void GoPage(int delta)
        {
            if (pages.Count == 0) return;
            int next = index + delta;
            if (next < 0 || next >= pages.Count) return;
            index = next;
            UpdateCaption();
            Invalidate();
        }

        private void SetZoom(float value)
        {
            zoom = Math.Max(0.3f, Math.Min(4f, value));
            Invalidate();
        }

        private void UpdateCaption()
        {
            if (pages.Count == 0) { lblCaption.Text = "没有可预览的页面"; return; }
            PreviewPage p = pages[index];
            Text = "打印预览 - " + (index + 1) + " / " + pages.Count;
            lblPageNo.Text = "第 " + (index + 1) + " / " + pages.Count + " 页";
            double pw, ph;
            PaperSizeMm(p.Page, out pw, out ph);
            lblCaption.Text = p.Label
                + "    图纸 " + p.Page.SizeLabel
                + "  →  纸张 " + PaperName()
                + (IsLandscape(p.Page) ? " 横向" : " 纵向")
                + "，可打印区四周 " + hardMmX.ToString("0.#", CultureInfo.InvariantCulture)
                + " / " + hardMmY.ToString("0.#", CultureInfo.InvariantCulture) + " mm"
                + "，安全边距 " + opt.MarginMm.ToString("0.#", CultureInfo.InvariantCulture) + " mm";
        }

        private string PaperName()
        {
            string mode = string.IsNullOrEmpty(opt.Paper) ? "auto" : opt.Paper;
            if (string.Equals(mode, "A3", StringComparison.OrdinalIgnoreCase)) return "A3";
            if (string.Equals(mode, "A4", StringComparison.OrdinalIgnoreCase)) return "A4";
            if (string.Equals(mode, "A5", StringComparison.OrdinalIgnoreCase)) return "A5";
            if (string.Equals(mode, "Letter", StringComparison.OrdinalIgnoreCase)) return "Letter";
            return pages.Count > 0 && Math.Max(pages[index].Page.WidthMm, pages[index].Page.HeightMm) > 310.0 ? "A3" : "A4";
        }

        private bool IsLandscape(PdfPageInfo page)
        {
            string o = string.IsNullOrEmpty(opt.Orientation) ? "auto" : opt.Orientation;
            if (string.Equals(o, "landscape", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(o, "portrait", StringComparison.OrdinalIgnoreCase)) return false;
            return page.WidthMm > page.HeightMm;
        }

        private void PaperSizeMm(PdfPageInfo page, out double w, out double h)
        {
            string name = PaperName();
            w = 210; h = 297;
            if (name == "A3") { w = 297; h = 420; }
            else if (name == "A5") { w = 148; h = 210; }
            else if (name == "Letter") { w = 215.9; h = 279.4; }
            if (IsLandscape(page)) { double t = w; w = h; h = t; }
        }

        private Image GetImage(int i)
        {
            Image img;
            if (cache.TryGetValue(i, out img)) return img;
            try
            {
                img = Image.FromFile(pages[i].ImagePath);
                cache[i] = img;
            }
            catch { img = null; }
            return img;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (pages.Count == 0) return;
            PreviewPage p = pages[index];

            double pw, ph;
            PaperSizeMm(p.Page, out pw, out ph);

            var area = new RectangleF(16f, lblCaption.Height + 14f,
                ClientSize.Width - 32f, ClientSize.Height - lblCaption.Height - 14f - 66f);
            if (area.Width < 60f || area.Height < 60f) return;

            float fit = Math.Min(area.Width / (float)pw, area.Height / (float)ph) * 0.96f;
            float scale = fit * zoom;
            float pageW = (float)pw * scale;
            float pageH = (float)ph * scale;
            float x = area.X + (area.Width - pageW) / 2f;
            float y = area.Y + (area.Height - pageH) / 2f;

            using (var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                e.Graphics.FillRectangle(shadow, x + 7f, y + 7f, pageW, pageH);
            using (var paper = new SolidBrush(Color.White))
                e.Graphics.FillRectangle(paper, x, y, pageW, pageH);
            using (var edge = new Pen(Color.FromArgb(130, 130, 130)))
                e.Graphics.DrawRectangle(edge, x, y, pageW, pageH);

            float hx = hardMmX * scale;
            float hy = hardMmY * scale;
            var printable = new RectangleF(x + hx, y + hy,
                Math.Max(12f, pageW - hx * 2f), Math.Max(12f, pageH - hy * 2f));
            float mx = (float)opt.MarginMm * scale;
            var inner = new RectangleF(printable.X + mx, printable.Y + mx,
                Math.Max(10f, printable.Width - mx * 2f), Math.Max(10f, printable.Height - mx * 2f));

            bool clipped = false;
            var pageRect = new RectangleF(x, y, pageW, pageH);
            GraphicsState state = e.Graphics.Save();
            e.Graphics.SetClip(pageRect);
            Image img = GetImage(index);
            if (img != null)
            {
                float iw = (float)(p.Page.WidthMm * scale);
                float ih = (float)(p.Page.HeightMm * scale);
                float w, h;
                if (string.Equals(opt.Scale, "actual", StringComparison.OrdinalIgnoreCase))
                {
                    w = iw;
                    h = ih;
                }
                else
                {
                    float f = Math.Min(inner.Width / Math.Max(1f, iw), inner.Height / Math.Max(1f, ih));
                    w = iw * f;
                    h = ih * f;
                }
                float dx = inner.X + (inner.Width - w) / 2f;
                float dy = inner.Y + (inner.Height - h) / 2f;
                var dest = new RectangleF(dx, dy, w, h);
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(img, dest);

                using (var outside = new Region(dest))
                {
                    outside.Exclude(printable);
                    outside.Intersect(pageRect);
                    RectangleF bad = outside.GetBounds(e.Graphics);
                    if (bad.Width > 0.5f || bad.Height > 0.5f)
                    {
                        clipped = true;
                        using (var red = new SolidBrush(Color.FromArgb(110, 230, 60, 60)))
                            e.Graphics.FillRegion(red, outside);
                        using (var warn = new Pen(Color.FromArgb(230, 60, 60), 2f))
                            e.Graphics.DrawRectangle(warn, dest.X, dest.Y, dest.Width, dest.Height);
                    }
                }
            }

            using (var pen = new Pen(Color.FromArgb(90, 120, 200), 1.4f))
            {
                pen.DashStyle = DashStyle.Dash;
                e.Graphics.DrawRectangle(pen, printable.X, printable.Y, printable.Width, printable.Height);
            }
            e.Graphics.Restore(state);
            string foot = "虚线 = 打印机可打印区，红块 = 会被裁掉的部分";
            if (clipped) foot = "注意：红色区域超出可打印区，打印时会被裁掉（可调大“边距”或改纸张）";
            using (var font = new Font("Microsoft YaHei UI", 9f))
            using (var brush = new SolidBrush(clipped ? Color.FromArgb(255, 170, 160) : Color.FromArgb(190, 190, 190)))
                e.Graphics.DrawString(foot, font, brush, 18f, ClientSize.Height - 52f - 28f);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
            else if (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Right) { GoPage(1); e.Handled = true; }
            else if (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.Left) { GoPage(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Home) { index = 0; UpdateCaption(); Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.End) { index = pages.Count - 1; UpdateCaption(); Invalidate(); e.Handled = true; }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                SetZoom(zoom + (e.Delta > 0 ? 0.1f : -0.1f));
                return;
            }
            base.OnMouseWheel(e);
            if (e.Delta > 0) GoPage(-1); else GoPage(1);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            foreach (Image img in cache.Values)
            {
                try { img.Dispose(); } catch { }
            }
            cache.Clear();
        }
    }
}
