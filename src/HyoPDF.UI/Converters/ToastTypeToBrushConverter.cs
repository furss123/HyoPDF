using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HyoPDF.UI.Services;

namespace HyoPDF.UI.Converters;

public sealed class ToastTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ToastType type
            ? type switch
            {
                ToastType.Success => (Brush)Application.Current.FindResource("AccentBrush"),
                ToastType.Error => (Brush)Application.Current.FindResource("ErrorBrush"),
                _ => (Brush)Application.Current.FindResource("InfoBrush")
            }
            : (Brush)Application.Current.FindResource("InfoBrush");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
