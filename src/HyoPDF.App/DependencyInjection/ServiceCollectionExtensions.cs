using HyoPDF.Core.Localization;
using HyoPDF.Core.Services;
using HyoPDF.Core.Settings;
using HyoPDF.Core.Themes;
using HyoPDF.Core.UndoRedo;
using HyoPDF.UI.Localization;
using HyoPDF.UI.Services;
using HyoPDF.UI.Themes;
using HyoPDF.UI.ViewModels;
using HyoPDF.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HyoPDF.App;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHyoPdfServices(this IServiceCollection services)
    {
        services.AddSingleton<IPageService, PageService>();
        services.AddSingleton<IPrintService, PrintService>();
        services.AddSingleton<ICompressService, CompressService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<ILocalSettingsStore, LocalSettingsStore>();
        services.AddSingleton<IUndoRedoStack, UndoRedoStack>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<TabItemFactory>();
        services.AddSingleton<ThemeManager>();

        services.AddSingleton<TabsViewModel>();
        services.AddTransient<PrintViewModel>();
        services.AddTransient<CompressViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
