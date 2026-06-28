using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Settings;
using HyoPDF.UI.Services;
using HyoPDF.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HyoPDF.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddHyoPdfServices())
            .Build();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[Global] {e.Exception}");
        e.Handled = true;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var settingsStore = _host.Services.GetRequiredService<ILocalSettingsStore>();
        var localization = _host.Services.GetRequiredService<ILocalizationService>();
        var settings = settingsStore.Load();
        localization.SetCulture(settings.Language);

        _host.Services.GetRequiredService<ThemeManager>();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        if (e.Args is { Length: > 0 } args)
        {
            var path = args[0];
            if (File.Exists(path))
                mainWindow.OpenFileFromPath(path);
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
