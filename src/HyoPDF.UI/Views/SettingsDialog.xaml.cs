using System.Windows;

namespace HyoPDF.UI.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog(object viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
