using System.Windows;

namespace HyoPDF.UI.Helpers;

public static class PrimaryMonitorPlacement
{
    public const double HdReferenceWidth = 1920;
    public const double HdReferenceHeight = 1080;

    private const double DefaultWidthOnHd = 1280;
    private const double DefaultHeightOnHd = 800;
    private const double MinWidth = 580;
    private const double MinHeight = 440;

    public static Rect WorkArea => SystemParameters.WorkArea;

    public static (double Width, double Height) GetDefaultSize()
    {
        var work = WorkArea;
        var width = Math.Min(DefaultWidthOnHd, work.Width * 0.9);
        var height = Math.Min(DefaultHeightOnHd, work.Height * 0.85);

        return (
            Math.Clamp(width, MinWidth, work.Width),
            Math.Clamp(height, MinHeight, work.Height));
    }

    public static (double Width, double Height) ClampSize(double width, double height)
    {
        var work = WorkArea;
        return (
            Math.Clamp(width, MinWidth, work.Width),
            Math.Clamp(height, MinHeight, work.Height));
    }

    public static void PlaceOnPrimaryMonitor(Window window)
    {
        var work = WorkArea;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = work.Left + Math.Max(0, (work.Width - window.Width) / 2);
        window.Top = work.Top + Math.Max(0, (work.Height - window.Height) / 2);
    }
}
