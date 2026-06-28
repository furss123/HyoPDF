using System.Windows;
using Microsoft.Win32;

namespace HyoPDF.UI.Helpers;

public static class FileDialogHelper
{
    public static bool? ShowOpenDialog(this OpenFileDialog dialog, Window? owner = null) =>
        ShowDialog(dialog, owner);

    public static bool? ShowSaveDialog(this SaveFileDialog dialog, Window? owner = null) =>
        ShowDialog(dialog, owner);

    private static bool? ShowDialog(FileDialog dialog, Window? owner)
    {
        owner ??= Application.Current?.MainWindow;
        if (owner is null)
            return dialog.ShowDialog();

        if (!owner.IsActive)
            owner.Activate();

        // Borderless WPF windows need an owned helper window for reliable modal file dialogs.
        var scope = new Window
        {
            Owner = owner,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Width = 0,
            Height = 0,
            Left = -20000,
            Top = -20000,
        };

        scope.Show();
        try
        {
            return dialog.ShowDialog(scope);
        }
        finally
        {
            scope.Close();
        }
    }
}
