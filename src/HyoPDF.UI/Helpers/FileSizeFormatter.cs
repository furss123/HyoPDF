using System.Globalization;
using HyoPDF.Core.Compression;

namespace HyoPDF.UI.Helpers;

public static class FileSizeFormatter
{
    public static string Format(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes:N0} {units[unit]}"
            : $"{size.ToString("0.##", CultureInfo.CurrentCulture)} {units[unit]}";
    }
}
