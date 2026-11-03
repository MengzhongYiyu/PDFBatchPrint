using System;
using System.Drawing;
using System.Drawing.Printing;

class P
{
    static int index = 0;
    static int pages = 3;
    static string mode = "0";

    static void Main(string[] a)
    {
        mode = a.Length > 0 ? a[0] : "0";
        string outPath = a.Length > 1 ? a[1] : "C:\\Windows\\Temp\\pdfbatch_test\\probe_out.pdf";
        var doc = new PrintDocument();
        doc.DocumentName = "probe";
        doc.PrintController = new StandardPrintController();
        doc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
        doc.PrinterSettings.PrintToFile = true;
        doc.PrinterSettings.PrintFileName = outPath;
        if (mode == "1" || mode == "3" || mode == "4")
        {
            doc.QueryPageSettings += delegate(object s, QueryPageSettingsEventArgs e)
            {
                e.PageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            };
        }
        if (mode == "2" || mode == "3")
        {
            doc.QueryPageSettings += delegate(object s, QueryPageSettingsEventArgs e)
            {
                e.PageSettings.Margins = new Margins(16, 16, 16, 16);
            };
        }
        if (mode == "4")
        {
            doc.QueryPageSettings += delegate(object s, QueryPageSettingsEventArgs e)
            {
                e.PageSettings.Landscape = false;
            };
        }
        doc.PrintPage += delegate(object s, PrintPageEventArgs e)
        {
            e.Graphics.DrawString("PAGE " + (index + 1) + " mode " + mode, new Font("Arial", 20), Brushes.Black, 100, 100);
            index++;
            e.HasMorePages = index < pages;
        };
        doc.Print();
        Console.WriteLine("done mode=" + mode);
    }
}