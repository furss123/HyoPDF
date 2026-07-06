using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HyoPDF.Core.Diagnostics;
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
    private SingleInstance? _singleInstance;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddHyoPdfServices())
            .Build();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[Global] {e.Exception}");
        FileLog.Write("[Global] Dispatcher unhandled exception", e.Exception);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        FileLog.Write("[Global] AppDomain unhandled exception", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        FileLog.Write("[Global] Unobserved task exception", e.Exception);
        e.SetObserved();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstance = SingleInstance.Acquire();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.TryForwardArguments(e.Args);
            Shutdown();
            return;
        }

        await _host.StartAsync();

        var settingsStore = _host.Services.GetRequiredService<ILocalSettingsStore>();
        var localization = _host.Services.GetRequiredService<ILocalizationService>();
        var settings = settingsStore.Load();
        localization.SetCulture(settings.Language);

        _host.Services.GetRequiredService<ThemeManager>();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        _singleInstance.StartListening(mainWindow.OpenFileWhenReady);
        mainWindow.Show();

        foreach (var path in e.Args.Where(SingleInstance.IsOpenablePath).Select(SingleInstance.NormalizePath))
        {
            if (File.Exists(path))
                mainWindow.OpenFileWhenReady(path);
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
