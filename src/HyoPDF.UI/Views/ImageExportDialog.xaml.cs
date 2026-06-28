using System.Windows;
using HyoPDF.UI.Models;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class ImageExportDialog : Window
{
    private readonly ImageExportViewModel _viewModel;

    public ImageExportDialog(ImageExportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public ImageExportOptions Result => _viewModel.ToOptions();

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanExport())
            return;

        DialogResult = true;
        Close();
    }
}
