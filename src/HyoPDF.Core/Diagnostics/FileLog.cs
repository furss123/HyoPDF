using System.Diagnostics;
using System.IO;

namespace HyoPDF.Core.Diagnostics;

public static class FileLog
{
    private static readonly object Gate = new();

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HyoPDF",
                "logs");
            Directory.CreateDirectory(dir);

            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}";
            if (exception is not null)
                line += Environment.NewLine + exception;

            lock (Gate)
            {
                File.AppendAllText(
                    Path.Combine(dir, "hyopdf.log"),
                    line + Environment.NewLine + Environment.NewLine);
            }

            Debug.WriteLine(line);
        }
        catch
        {
            // Logging must never affect the app.
        }
    }
}
