using System.Windows;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class PrintDialog : Window
{
    public PrintDialog(PrintViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
