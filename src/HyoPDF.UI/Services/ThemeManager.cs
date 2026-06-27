using System.Windows;
using HyoPDF.Core.Themes;

namespace HyoPDF.UI.Services;

public sealed class ThemeManager
{
    private readonly IThemeService _themeService;
    private readonly ResourceDictionary _lightTheme;
    private readonly ResourceDictionary _darkTheme;

    public ThemeManager(IThemeService themeService)
    {
        _themeService = themeService;
        _lightTheme = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HyoPDF.UI;component/Themes/LightTheme.xaml")
        };
        _darkTheme = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/HyoPDF.UI;component/Themes/DarkTheme.xaml")
        };

        _themeService.ThemeChanged += (_, theme) => Apply(theme);
    }

    public void Initialize()
    {
        _themeService.ApplySystemTheme();
    }

    private void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        var dictionaries = app.Resources.MergedDictionaries;
        var themeDict = theme == AppTheme.Dark ? _darkTheme : _lightTheme;

        var existing = dictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Themes/") == true);

        if (existing is not null)
            dictionaries.Remove(existing);

        dictionaries.Add(themeDict);
    }
}
