using System.Drawing;
using System.Drawing.Printing;
using HyoPDF.Core.Printing;
using PdfiumViewer;

namespace HyoPDF.Core.Services;

public sealed class PrintService : IPrintService
{
    private const float PrintDpi = 150f;

    public IReadOnlyList<string> GetInstalledPrinters()
    {
        var printers = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
            printers.Add(printer);
        return printers;
    }

    public void PrintDocument(PrintOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FilePath);

        using var pdf = PdfDocument.Load(options.FilePath);
        var pageCount = pdf.PageCount;
        var pageIndices = options.PageRange is { Length: > 0 } range
            ? range.Where(i => i >= 0 && i < pageCount).Distinct().OrderBy(i => i).ToArray()
            : Enumerable.Range(0, pageCount).ToArray();

        if (pageIndices.Length == 0)
            throw new InvalidOperationException("No pages selected for printing.");

        var sheets = PrintLayoutHelper.GroupIntoSheets(pageIndices, options.PagesPerSheet);
        var (columns, rows) = PrintLayoutHelper.GetGrid(options.PagesPerSheet);

        using var printDocument = new PrintDocument();
        ConfigurePrinter(printDocument, options);

        var sheetIndex = 0;
        printDocument.PrintPage += (_, e) =>
        {
            if (sheetIndex >= sheets.Count)
            {
                e.HasMorePages = false;
                return;
            }

            var graphics = e.Graphics ?? throw new InvalidOperationException("Print graphics unavailable.");
            var bounds = e.MarginBounds;
            DrawSheet(graphics, bounds, pdf, sheets[sheetIndex], columns, rows, options.FitToPage);
            sheetIndex++;
            e.HasMorePages = sheetIndex < sheets.Count;
        };

        if (options.Preview)
        {
            using var preview = new System.Windows.Forms.PrintPreviewDialog
            {
                Document = printDocument,
                Width = 900,
                Height = 700
            };
            preview.ShowDialog();
        }
        else
        {
            printDocument.Print();
        }
    }

    private static void ConfigurePrinter(PrintDocument printDocument, PrintOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PrinterName))
            printDocument.PrinterSettings.PrinterName = options.PrinterName;

        printDocument.PrinterSettings.Copies = (short)Math.Clamp(options.Copies, 1, short.MaxValue);
        printDocument.PrinterSettings.Duplex = options.Duplex ? Duplex.Vertical : Duplex.Simplex;
    }

    private static void DrawSheet(
        Graphics graphics,
        Rectangle bounds,
        PdfDocument pdf,
        int[] pageIndices,
        int columns,
        int rows,
        bool fitToPage)
    {
        var cellWidth = bounds.Width / columns;
        var cellHeight = bounds.Height / rows;
        var padding = 4;

        for (var i = 0; i < pageIndices.Length; i++)
        {
            var col = i % columns;
            var row = i / columns;
            var cell = new Rectangle(
                bounds.Left + col * cellWidth + padding,
                bounds.Top + row * cellHeight + padding,
                cellWidth - padding * 2,
                cellHeight - padding * 2);

            using var image = pdf.Render(pageIndices[i], PrintDpi, PrintDpi, PdfRenderFlags.Annotations);
            DrawImage(graphics, image, cell, fitToPage);
        }
    }

    private static void DrawImage(Graphics graphics, Image image, Rectangle target, bool fitToPage)
    {
        if (fitToPage)
        {
            graphics.DrawImage(image, target);
            return;
        }

        var scale = Math.Min((float)target.Width / image.Width, (float)target.Height / image.Height);
        var width = (int)(image.Width * scale);
        var height = (int)(image.Height * scale);
        var x = target.Left + (target.Width - width) / 2;
        var y = target.Top + (target.Height - height) / 2;
        graphics.DrawImage(image, x, y, width, height);
    }
}
