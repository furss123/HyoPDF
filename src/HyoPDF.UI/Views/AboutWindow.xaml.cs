using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using HyoPDF.Core.Localization;

namespace HyoPDF.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(ILocalizationService localization)
    {
        InitializeComponent();
        Title = localization.GetString("AboutTitle");
        OkButton.Content = localization.GetString("Ok");
        DataContext = new AboutViewModel(localization);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}

public sealed class AboutViewModel
{
    public string FooterText { get; }

    public AboutViewModel(ILocalizationService localization)
    {
        FooterText = $"HyoPDF v{GetProductVersion()} · © 2026 HyoT. All rights reserved. · ";
    }

    private static string GetProductVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        if (assembly is null)
            return "1.0.0";

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational;

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(processPath).ProductVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
                return fileVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
