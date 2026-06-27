using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using HyoPDF.Core.Compression;
using iTextSharp.text.pdf;
using DrawingImage = System.Drawing.Image;
using PdfImage = iTextSharp.text.Image;

namespace HyoPDF.Core.Services;

public sealed class CompressService : ICompressService
{
    public long EstimateCompressedSize(long originalSize, CompressionLevel level) =>
        (long)Math.Max(1, originalSize * level.EstimatedSizeRatio());

    public void CompressPdf(
        string inputPath,
        string outputPath,
        CompressionLevel level,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var tempPath = outputPath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        PdfReader? reader = null;
        PdfStamper? stamper = null;

        try
        {
            reader = new PdfReader(inputPath);
            using var outputStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
            stamper = new PdfStamper(reader, outputStream, PdfWriter.VERSION_1_5);

            var writer = stamper.Writer;
            writer.CompressionLevel = GetStreamCompression(level);
            writer.SetFullCompression();

            var (jpegQuality, maxDimension) = GetImageSettings(level);
            var processed = new HashSet<int>();
            var total = reader.XrefSize;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.GetPdfObjectRelease(i) is PdfIndirectReference indirect)
                    TryReplaceImageStream(reader, stamper, indirect, jpegQuality, maxDimension, processed);

                if (i % 16 == 0 || i == total - 1)
                    progress?.Report((i + 1) * 100.0 / total);
            }

            stamper.Close();
            stamper = null;
            reader.Close();
            reader = null;

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(tempPath, outputPath);
            progress?.Report(100);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
        finally
        {
            stamper?.Close();
            reader?.Close();
        }
    }

    private static void TryReplaceImageStream(
        PdfReader reader,
        PdfStamper stamper,
        PdfIndirectReference indirect,
        long jpegQuality,
        int maxDimension,
        ISet<int> processed)
    {
        if (!processed.Add(indirect.Number))
            return;

        try
        {
            var pdfObject = PdfReader.GetPdfObject(indirect);
            if (pdfObject is not PrStream stream)
                return;

            var subtype = stream.GetAsName(PdfName.Subtype);
            if (!PdfName.Image.Equals(subtype))
                return;

            var bytes = PdfReader.GetStreamBytes(stream);
            using var inputStream = new MemoryStream(bytes);
            using var drawingImage = DrawingImage.FromStream(inputStream);
            using var resized = ResizeImage(drawingImage, maxDimension);
            var jpegBytes = ImageToJpegBytes(resized, jpegQuality);
            var replacement = PdfImage.GetInstance(jpegBytes);

            PdfReader.KillIndirect(stream);
            stamper.Writer.AddDirectImageSimple(replacement, indirect);
        }
        catch
        {
            // Keep original image if recompression fails.
        }
    }

    private static DrawingImage ResizeImage(DrawingImage source, int maxDimension)
    {
        var width = source.Width;
        var height = source.Height;
        var longest = Math.Max(width, height);

        if (longest <= maxDimension)
            return (DrawingImage)source.Clone();

        var scale = maxDimension / (double)longest;
        var targetWidth = Math.Max(1, (int)(width * scale));
        var targetHeight = Math.Max(1, (int)(height * scale));

        var bitmap = new Bitmap(targetWidth, targetHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, 0, 0, targetWidth, targetHeight);
        return bitmap;
    }

    private static byte[] ImageToJpegBytes(DrawingImage image, long quality)
    {
        using var stream = new MemoryStream();
        var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        image.Save(stream, encoder, parameters);
        return stream.ToArray();
    }

    private static int GetStreamCompression(CompressionLevel level) => level switch
    {
        CompressionLevel.Level1 => PdfStream.NO_COMPRESSION,
        CompressionLevel.Level2 => 3,
        CompressionLevel.Level3 => 5,
        CompressionLevel.Level4 => 7,
        CompressionLevel.Level5 => PdfStream.BEST_COMPRESSION,
        _ => 5
    };

    private static (long JpegQuality, int MaxDimension) GetImageSettings(CompressionLevel level) => level switch
    {
        CompressionLevel.Level1 => (85L, 2400),
        CompressionLevel.Level2 => (70L, 1800),
        CompressionLevel.Level3 => (55L, 1400),
        CompressionLevel.Level4 => (40L, 1000),
        CompressionLevel.Level5 => (25L, 800),
        _ => (55L, 1400)
    };
}
