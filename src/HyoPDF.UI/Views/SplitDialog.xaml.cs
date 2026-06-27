using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace HyoPDF.UI.Views;

public partial class SplitDialog : Window
{
    public IReadOnlyList<int> SplitPoints { get; private set; } = [];
    public string OutputDirectory { get; private set; } = string.Empty;

    public SplitDialog(string? defaultOutputDir)
    {
        InitializeComponent();
        OutputDirBox.Text = defaultOutputDir ?? string.Empty;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        if (dialog.ShowDialog() == true)
            OutputDirBox.Text = dialog.FolderName;
    }

    private void OnSplit(object sender, RoutedEventArgs e)
    {
        var outputDir = OutputDirBox.Text.Trim();
        if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
        {
            MessageBox.Show(this, "Select a valid output folder.", "Split", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var points = SplitPointsBox.Text
            .Split([',', ';', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : -1)
            .Where(n => n > 0)
            .Select(n => n - 1)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (points.Count == 0)
        {
            MessageBox.Show(this, "Enter at least one valid page number.", "Split", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SplitPoints = points;
        OutputDirectory = outputDir;
        DialogResult = true;
    }
}
