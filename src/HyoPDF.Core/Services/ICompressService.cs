using HyoPDF.Core.Compression;

namespace HyoPDF.Core.Services;

public interface ICompressService
{
    void CompressPdf(
        string inputPath,
        string outputPath,
        CompressionLevel level,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    long EstimateCompressedSize(long originalSize, CompressionLevel level);
}
