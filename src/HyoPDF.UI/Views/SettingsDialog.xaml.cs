using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HyoPDF.UI.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (Keyboard.FocusedElement is ComboBox comboBox && comboBox.IsDropDownOpen)
        {
            comboBox.IsDropDownOpen = false;
            e.Handled = true;
            return;
        }

        Close();
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
