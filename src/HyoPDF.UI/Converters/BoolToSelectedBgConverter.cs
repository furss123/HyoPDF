using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HyoPDF.UI.Converters;

public sealed class BoolToSelectedBgConverter : IValueConverter
{
    private static readonly SolidColorBrush Selected = CreateFrozen(0x00, 0x78, 0xD4, 0x18);
    private static readonly SolidColorBrush Transparent = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Selected : Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush CreateFrozen(byte r, byte g, byte b, byte a)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
