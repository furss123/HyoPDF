using System.Globalization;
using System.Windows.Data;
using HyoPDF.Core.Settings;

namespace HyoPDF.UI.Converters;

public sealed class ThemePreferenceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is AppThemePreference theme
            ? theme switch
            {
                AppThemePreference.System => "System",
                AppThemePreference.Light => "Light",
                AppThemePreference.Dark => "Dark",
                _ => theme.ToString()
            }
            : value?.ToString() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
