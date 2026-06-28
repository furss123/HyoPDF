using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class PrintDialog : Window
{
    private bool _dialogResultSet;
    private PrintViewModel? _viewModel;

    public PrintDialog(PrintViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        viewModel.CloseRequested += OnPrintCompleted;
        viewModel.CancelRequested += OnCancelRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnWindowClosed;
        Loaded += OnWindowLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        CloseDialog(false);
        e.Handled = true;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e) =>
        ApplyComboBoxPopupColors();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PrintViewModel.IsDarkTheme))
            ApplyComboBoxPopupColors();
    }

    private void ApplyComboBoxPopupColors()
    {
        var isDark = _viewModel?.IsDarkTheme ?? true;
        ApplyComboBoxPopupColors(PrinterComboBox, isDark);
        ApplyComboBoxPopupColors(PagesPerSheetComboBox, isDark);
    }

    private static void ApplyComboBoxPopupColors(ComboBox comboBox, bool isDark)
    {
        var windowBg = isDark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
        var windowText = isDark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x1A, 0x1A, 0x1A);
        var highlightBg = isDark ? Color.FromRgb(0x2A, 0x2A, 0x2A) : Color.FromRgb(0xE8, 0xE8, 0xE8);
        var highlightText = isDark ? Colors.White : Color.FromRgb(0x1A, 0x1A, 0x1A);

        comboBox.Resources[SystemColors.WindowBrushKey] = new SolidColorBrush(windowBg);
        comboBox.Resources[SystemColors.WindowTextBrushKey] = new SolidColorBrush(windowText);
        comboBox.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(highlightBg);
        comboBox.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(highlightText);
    }

    private void PrintStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.PrintCommand.CanExecute(null) == true)
            _viewModel.PrintCommand.Execute(null);
    }

    private void OnCancelRequested(object? sender, EventArgs e) => CloseDialog(false);

    private void OnPrintCompleted(object? sender, EventArgs e) => CloseDialog(true);

    private void CloseDialog(bool success)
    {
        if (_dialogResultSet)
            return;

        _dialogResultSet = true;
        DialogResult = success;
        Close();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_viewModel is null)
            return;

        _viewModel.CloseRequested -= OnPrintCompleted;
        _viewModel.CancelRequested -= OnCancelRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }
}
