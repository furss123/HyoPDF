using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyoPDF.UI.Converters;

public sealed class SidebarToggleMarginConverter : IMultiValueConverter
{
    private const double ToggleWidth = 20;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var expanded = values.Length > 0 && values[0] is true;
        var sidebarWidth = values.Length > 1 && values[1] is double width ? width : 0d;

        if (expanded && sidebarWidth <= 0)
            sidebarWidth = 160;

        return expanded
            ? new Thickness(Math.Max(0, sidebarWidth - ToggleWidth), 0, 0, 0)
            : new Thickness(0, 0, 0, 0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
