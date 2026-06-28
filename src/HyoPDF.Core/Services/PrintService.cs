using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Printing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HyoPDF.Core.Printing;
using PdfiumViewer;namespace HyoPDF.Core.Services;

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

    public string? GetDefaultPrinterName()
    {
        try
        {
            return new PrinterSettings().PrinterName;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] Default printer lookup failed: {ex.Message}");
            return null;
        }
    }

    public void ShowPrinterSettingsDialog(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return;

        try
        {
            using var document = new PrintDocument { PrinterSettings = { PrinterName = printerName } };
            using var dialog = new System.Windows.Forms.PrintDialog
            {
                Document = document,
                AllowPrintToFile = false,
                AllowSelection = false,
                UseEXDialog = true
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] Printer settings: {ex}");
        }
    }

    public void PrintViaNative(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The document file could not be found.", filePath);

        var info = new ProcessStartInfo
        {
            FileName = filePath,
            Verb = "print",
            UseShellExecute = true
        };

        try
        {
            // Shell "print" often returns null even on success — no process handle is created.
            var proc = Process.Start(info);
            Debug.WriteLine(proc is null
                ? "[Print] Shell print verb launched (no process handle returned)."
                : $"[Print] Process started: {proc.Id}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] Native print failed: {ex}");
            throw new InvalidOperationException(
                "Windows에서 PDF 인쇄를 시작할 수 없습니다. 기본 PDF 앱(Edge, Adobe 등)이 설치되어 있는지 확인하세요.",
                ex);
        }
    }

    public bool TryPrintViaNative(string filePath)
    {
        try
        {
            PrintViaNative(filePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] TryPrintViaNative: {ex.Message}");
            return false;
        }
    }

    public async Task PrintWithWpfOptionsAsync(
        string filePath,
        PrintOptions options,
        System.Windows.Controls.PrintDialog printDialog,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(printDialog);

        if (!File.Exists(filePath))
            throw new FileNotFoundException("The document file could not be found.", filePath);

        ApplyPrintTicket(printDialog, options);

        var printableWidth = printDialog.PrintableAreaWidth;
        var printableHeight = printDialog.PrintableAreaHeight;
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Application dispatcher is not available.");

        var renderFlags = GetRenderFlags(options.Grayscale);

        await Task.Run(() =>
        {
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var pdf = PdfDocument.Load(fileStream);

            var pageIndices = ResolvePageIndices(options, pdf.PageCount);
            if (pageIndices.Length == 0)
                throw new InvalidOperationException("No pages selected for printing.");

            var sheets = PrintLayoutHelper.GroupIntoSheets(pageIndices, options.PagesPerSheet);
            var (columns, rows) = PrintLayoutHelper.GetGrid(options.PagesPerSheet);
            var fitMode = options.FitMode;
            if (options.FitToPage && fitMode == PrintFitMode.FitToPage)
                fitMode = PrintFitMode.FitToPage;

            for (var sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sheetPages = sheets[sheetIndex];
                var images = new ImageSource[sheetPages.Length];
                for (var i = 0; i < sheetPages.Length; i++)
                {
                    using var rendered = pdf.Render(sheetPages[i], PrintDpi, PrintDpi, renderFlags);
                    using var bitmap = rendered is Bitmap bmp ? bmp : new Bitmap(rendered);
                    var source = BitmapToBitmapSource(bitmap);
                    source.Freeze();
                    images[i] = source;
                }

                var currentSheet = sheetIndex + 1;
                dispatcher.Invoke(() =>
                {
                    var visual = CreateSheetVisual(
                        images,
                        columns,
                        rows,
                        printableWidth,
                        printableHeight,
                        fitMode,
                        options.CustomScale);
                    printDialog.PrintVisual(visual, $"HyoPDF - {currentSheet}/{sheets.Count}");
                });
            }
        }, cancellationToken).ConfigureAwait(true);
    }

    public async Task<ImageSource?> RenderPagePreviewAsync(
        string filePath,
        int pageIndex,
        bool grayscale,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath) || pageIndex < 0)
            return null;

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var pdf = PdfDocument.Load(fileStream);

            if (pageIndex >= pdf.PageCount)
                return null;

            using var rendered = pdf.Render(pageIndex, PrintDpi, PrintDpi, GetRenderFlags(grayscale));
            using var bitmap = rendered is Bitmap bmp ? bmp : new Bitmap(rendered);
            var source = BitmapToBitmapSource(bitmap);
            source.Freeze();
            return (ImageSource)source;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static PdfRenderFlags GetRenderFlags(bool grayscale)
    {
        var flags = PdfRenderFlags.Annotations;
        if (grayscale)
            flags |= PdfRenderFlags.Grayscale;
        return flags;
    }

    private static int[] ResolvePageIndices(PrintOptions options, int pageCount)
    {
        if (options.PageRange is { Length: > 0 } range)
            return range.Where(i => i >= 0 && i < pageCount).Distinct().OrderBy(i => i).ToArray();

        return pageCount > 0 ? Enumerable.Range(0, pageCount).ToArray() : [];
    }

    private static void ApplyPrintTicket(System.Windows.Controls.PrintDialog printDialog, PrintOptions options)
    {
        var ticket = printDialog.PrintTicket ?? printDialog.PrintQueue?.DefaultPrintTicket?.Clone();
        if (ticket is null)
            return;

        ticket.CopyCount = Math.Clamp(options.Copies, 1, 999);
        ticket.Collation = options.Collate ? Collation.Collated : Collation.Uncollated;
        ticket.Duplexing = options.Duplex ? Duplexing.TwoSidedLongEdge : Duplexing.OneSided;
        ticket.OutputColor = options.Grayscale ? OutputColor.Grayscale : OutputColor.Color;
        printDialog.PrintTicket = ticket;
    }

    private static DrawingVisual CreateSheetVisual(
        IReadOnlyList<ImageSource> pageImages,
        int columns,
        int rows,
        double printableWidth,
        double printableHeight,
        PrintFitMode fitMode,
        int customScale)
    {
        var visual = new DrawingVisual();
        using var context = visual.RenderOpen();

        var cellWidth = printableWidth / Math.Max(1, columns);
        var cellHeight = printableHeight / Math.Max(1, rows);
        const double padding = 4;

        for (var i = 0; i < pageImages.Count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var target = new System.Windows.Rect(
                column * cellWidth + padding,
                row * cellHeight + padding,
                Math.Max(1, cellWidth - padding * 2),
                Math.Max(1, cellHeight - padding * 2));

            DrawImageInRect(context, pageImages[i], target, fitMode, customScale);
        }

        return visual;
    }

    private static void DrawImageInRect(
        DrawingContext context,
        ImageSource image,
        System.Windows.Rect target,
        PrintFitMode fitMode,
        int customScale)
    {
        if (image.Width <= 0 || image.Height <= 0)
            return;

        var drawRect = fitMode switch
        {
            PrintFitMode.ActualSize => GetActualSizeRect(image, target),
            PrintFitMode.Custom => ScaleRect(GetActualSizeRect(image, target), customScale / 100.0, target),
            _ => FitRect(image, target)
        };

        context.DrawImage(image, drawRect);
    }

    private static System.Windows.Rect FitRect(ImageSource image, System.Windows.Rect target)
    {
        var scale = Math.Min(target.Width / image.Width, target.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        var x = target.X + (target.Width - width) / 2;
        var y = target.Y + (target.Height - height) / 2;
        return new System.Windows.Rect(x, y, width, height);
    }

    private static System.Windows.Rect GetActualSizeRect(ImageSource image, System.Windows.Rect target)
    {
        var width = image.Width / PrintDpi * 96.0;
        var height = image.Height / PrintDpi * 96.0;
        var x = target.X + (target.Width - width) / 2;
        var y = target.Y + (target.Height - height) / 2;
        return new System.Windows.Rect(x, y, width, height);
    }

    private static System.Windows.Rect ScaleRect(System.Windows.Rect rect, double scale, System.Windows.Rect bounds)
    {
        var width = rect.Width * scale;
        var height = rect.Height * scale;
        var x = bounds.X + (bounds.Width - width) / 2;
        var y = bounds.Y + (bounds.Height - height) / 2;
        return new System.Windows.Rect(x, y, width, height);
    }

    private static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        return image;
    }

    public void PrintDocument(PrintOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.FilePath))
            throw new InvalidOperationException("No document path specified for printing.");

        if (!File.Exists(options.FilePath))
            throw new FileNotFoundException("The document file could not be found.", options.FilePath);

        try
        {
            PrintDocumentCore(options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] Full error: {ex}");
            throw;
        }
    }

    private static void PrintDocumentCore(PrintOptions options)
    {
        using var fileStream = new FileStream(
            options.FilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var pdf = PdfDocument.Load(fileStream);
        var pageCount = pdf.PageCount;
        var pageIndices = options.PageRange is { Length: > 0 } range
            ? range.Where(i => i >= 0 && i < pageCount).Distinct().OrderBy(i => i).ToArray()
            : Enumerable.Range(0, pageCount).ToArray();

        if (pageIndices.Length == 0)
            throw new InvalidOperationException("No pages selected for printing.");

        var sheets = PrintLayoutHelper.GroupIntoSheets(pageIndices, options.PagesPerSheet);
        var (columns, rows) = PrintLayoutHelper.GetGrid(options.PagesPerSheet);
        var renderedPages = PrerenderPages(pdf, pageIndices);

        try
        {
            using var printDocument = new PrintDocument();
            ConfigurePrinter(printDocument, options);

            var sheetIndex = 0;
            Exception? printPageError = null;

            PrintPageEventHandler? handler = null;
            handler = (_, e) =>
            {
                if (printPageError is not null)
                {
                    e.Cancel = true;
                    e.HasMorePages = false;
                    return;
                }

                if (sheetIndex >= sheets.Count)
                {
                    e.HasMorePages = false;
                    return;
                }

                try
                {
                    var graphics = e.Graphics ?? throw new InvalidOperationException("Print graphics unavailable.");
                    var bounds = e.MarginBounds;
                    DrawSheet(graphics, bounds, renderedPages, sheets[sheetIndex], columns, rows, options.FitToPage);
                    sheetIndex++;
                    e.HasMorePages = sheetIndex < sheets.Count;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PrintPage] {ex}");
                    printPageError = ex;
                    e.Cancel = true;
                    e.HasMorePages = false;
                }
            };

            printDocument.PrintPage -= handler;
            printDocument.PrintPage += handler;

            try
            {
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
                else if (options.ShowPrintDialog)
                {
                    using var dialog = new System.Windows.Forms.PrintDialog
                    {
                        Document = printDocument,
                        UseEXDialog = true,
                        AllowSomePages = false
                    };

                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;
                }

                if (!options.Preview)
                    printDocument.Print();
            }
            finally
            {
                printDocument.PrintPage -= handler;
            }

            if (printPageError is not null)
                throw printPageError;
        }
        finally
        {
            foreach (var image in renderedPages.Values)
                image.Dispose();
        }
    }

    private static Dictionary<int, Image> PrerenderPages(PdfDocument pdf, IReadOnlyList<int> pageIndices)
    {
        var cache = new Dictionary<int, Image>();
        foreach (var pageIndex in pageIndices.Distinct())
        {
            using var rendered = pdf.Render(pageIndex, PrintDpi, PrintDpi, PdfRenderFlags.Annotations);
            cache[pageIndex] = new Bitmap(rendered);
        }

        return cache;
    }

    private static void ConfigurePrinter(PrintDocument printDocument, PrintOptions options)
    {
        var printerName = options.PrinterName;
        if (string.IsNullOrWhiteSpace(printerName))
        {
            try
            {
                printerName = new PrinterSettings().PrinterName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Print] Default printer lookup failed: {ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(printerName))
            printDocument.PrinterSettings.PrinterName = printerName;

        if (!printDocument.PrinterSettings.IsValid)
            throw new InvalidOperationException("설치된 프린터가 없거나 선택한 프린터를 사용할 수 없습니다.");

        printDocument.PrinterSettings.Copies = (short)Math.Clamp(options.Copies, 1, short.MaxValue);
        printDocument.PrinterSettings.Duplex = options.Duplex ? Duplex.Vertical : Duplex.Simplex;
    }

    private static void DrawSheet(
        Graphics graphics,
        Rectangle bounds,
        IReadOnlyDictionary<int, Image> renderedPages,
        int[] pageIndices,
        int columns,
        int rows,
        bool fitToPage)
    {
        var cellWidth = Math.Max(1, bounds.Width / columns);
        var cellHeight = Math.Max(1, bounds.Height / rows);
        var padding = 4;

        for (var i = 0; i < pageIndices.Length; i++)
        {
            if (!renderedPages.TryGetValue(pageIndices[i], out var image))
                continue;

            var col = i % columns;
            var row = i / columns;
            var cell = new Rectangle(
                bounds.Left + col * cellWidth + padding,
                bounds.Top + row * cellHeight + padding,
                Math.Max(1, cellWidth - padding * 2),
                Math.Max(1, cellHeight - padding * 2));

            DrawImage(graphics, image, cell, fitToPage);
        }
    }

    private static void DrawImage(Graphics graphics, Image image, Rectangle target, bool fitToPage)
    {
        if (image.Width <= 0 || image.Height <= 0)
            return;

        if (fitToPage)
        {
            graphics.DrawImage(image, target);
            return;
        }

        var scale = Math.Min((float)target.Width / image.Width, (float)target.Height / image.Height);
        if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale))
            return;

        var width = (int)(image.Width * scale);
        var height = (int)(image.Height * scale);
        var x = target.Left + (target.Width - width) / 2;
        var y = target.Top + (target.Height - height) / 2;
        graphics.DrawImage(image, x, y, width, height);
    }
}
