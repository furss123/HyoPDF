namespace HyoPDF.Core.Compression;

public enum CompressionLevel
{
    Level1,
    Level2,
    Level3,
    Level4,
    Level5
}

public static class CompressionLevelExtensions
{
    public static double EstimatedSizeRatio(this CompressionLevel level) => level switch
    {
        CompressionLevel.Level1 => 0.90,
        CompressionLevel.Level2 => 0.70,
        CompressionLevel.Level3 => 0.50,
        CompressionLevel.Level4 => 0.35,
        CompressionLevel.Level5 => 0.20,
        _ => 0.50
    };

    public static int ToSliderValue(this CompressionLevel level) => level switch
    {
        CompressionLevel.Level1 => 1,
        CompressionLevel.Level2 => 2,
        CompressionLevel.Level3 => 3,
        CompressionLevel.Level4 => 4,
        CompressionLevel.Level5 => 5,
        _ => 3
    };

    public static CompressionLevel FromSliderValue(int value) => value switch
    {
        1 => CompressionLevel.Level1,
        2 => CompressionLevel.Level2,
        3 => CompressionLevel.Level3,
        4 => CompressionLevel.Level4,
        5 => CompressionLevel.Level5,
        _ => CompressionLevel.Level3
    };
}
