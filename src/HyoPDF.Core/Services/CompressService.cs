using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using HyoPDF.Core.Compression;
using iTextSharp.text.pdf;
using DrawingImage = System.Drawing.Image;

namespace HyoPDF.Core.Services;

public sealed class CompressService : ICompressService
{
    private const float ReferenceDpi = 150f;

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

        var tempInput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");

        try
        {
            CopySourceFile(inputPath, tempInput);
            CompressPdfFromFile(tempInput, tempOutput, level, progress, cancellationToken);

            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(tempOutput, outputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempInput))
                File.Delete(tempInput);

            if (File.Exists(tempOutput))
                File.Delete(tempOutput);
        }
    }

    private static void CompressPdfFromFile(
        string inputPath,
        string outputPath,
        CompressionLevel level,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (level == CompressionLevel.Level5)
        {
            File.Copy(inputPath, outputPath, overwrite: true);
            progress?.Report(100);
            return;
        }

        CompressWithIText(inputPath, outputPath, level, progress, cancellationToken);
    }

    private static void CopySourceFile(string sourcePath, string destPath)
    {
        const int bufferSize = 1024 * 1024;
        using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var output = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        input.CopyTo(output, bufferSize);
    }

    private static void CompressWithIText(
        string inputPath,
        string outputPath,
        CompressionLevel level,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        PdfReader? reader = null;
        PdfStamper? stamper = null;

        try
        {
            reader = new PdfReader(inputPath);
            using var outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            stamper = new PdfStamper(reader, outputStream, PdfWriter.VERSION_1_5);

            stamper.Writer.CompressionLevel = PdfStream.BEST_COMPRESSION;
            stamper.Writer.SetFullCompression();

            var (jpegQuality, dpi, downscaleImages) = GetCompressionSettings(level);
            var pageCount = reader.NumberOfPages;

            for (var page = 1; page <= pageCount; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageDict = reader.GetPageN(page);
                var resources = pageDict.GetAsDict(PdfName.Resources);
                if (resources is null)
                    continue;

                var xObjects = resources.GetAsDict(PdfName.Xobject);
                if (xObjects is null)
                    continue;

                foreach (var key in xObjects.Keys)
                {
                    if (xObjects.GetDirectObject(key) is not PrStream stream)
                        continue;

                    var subtype = stream.GetAsName(PdfName.Subtype);
                    if (!PdfName.Image.Equals(subtype))
                        continue;

                    TryRecompressImageStream(stream, jpegQuality, dpi, downscaleImages);
                }

                progress?.Report(page * 100.0 / pageCount);
            }

            stamper.Close();
            stamper = null;
            reader.Close();
            reader = null;

            progress?.Report(100);
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            throw;
        }
        finally
        {
            stamper?.Close();
            reader?.Close();
        }
    }

    private static void TryRecompressImageStream(
        PrStream stream,
        int jpegQuality,
        int dpi,
        bool downscaleImages)
    {
        try
        {
            var imgBytes = PdfReader.GetStreamBytesRaw(stream);
            if (imgBytes is null || imgBytes.Length == 0)
                return;

            using var inputStream = new MemoryStream(imgBytes);
            using var image = DrawingImage.FromStream(inputStream);

            var newWidth = image.Width;
            var newHeight = image.Height;

            if (downscaleImages)
            {
                var scale = dpi / ReferenceDpi;
                newWidth = Math.Max(1, (int)(image.Width * scale));
                newHeight = Math.Max(1, (int)(image.Height * scale));
            }

            using var bitmap = new Bitmap(newWidth, newHeight);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            using var outputStream = new MemoryStream();
            var encoder = GetJpegEncoder();
            using var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
            bitmap.Save(outputStream, encoder, encoderParams);

            var compressed = outputStream.ToArray();
            stream.SetData(compressed, false, PdfStream.BEST_COMPRESSION);
            stream.Put(PdfName.Filter, PdfName.Dctdecode);
            stream.Put(PdfName.Width, new PdfNumber(newWidth));
            stream.Put(PdfName.Height, new PdfNumber(newHeight));
            stream.Put(PdfName.Bitspercomponent, new PdfNumber(8));
            stream.Put(PdfName.Colorspace, PdfName.Devicergb);
        }
        catch
        {
            // Keep original image if recompression fails.
        }
    }

    private static ImageCodecInfo GetJpegEncoder() =>
        ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == "image/jpeg");

    private static (int JpegQuality, int Dpi, bool DownscaleImages) GetCompressionSettings(CompressionLevel level) =>
        level switch
        {
            CompressionLevel.Level1 => (15, 72, true),
            CompressionLevel.Level2 => (35, 96, true),
            CompressionLevel.Level3 => (55, 120, true),
            CompressionLevel.Level4 => (75, 150, false),
            CompressionLevel.Level5 => (92, 200, false),
            _ => (55, 120, true)
        };
}
