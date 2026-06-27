using HyoPDF.Core.Settings;
using HyoPDF.Core.Themes;
using Windows.UI.ViewManagement;

namespace HyoPDF.UI.Themes;

public sealed class ThemeService : IThemeService
{
    private readonly UISettings _uiSettings = new();
    private AppThemePreference _preference = AppThemePreference.System;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public AppThemePreference Preference => _preference;

    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService()
    {
        _uiSettings.ColorValuesChanged += (_, _) =>
        {
            if (_preference == AppThemePreference.System)
                ApplySystemTheme();
        };
    }

    public AppTheme GetSystemTheme()
    {
        var background = _uiSettings.GetColorValue(UIColorType.Background);
        return background.R < 128 ? AppTheme.Dark : AppTheme.Light;
    }

    public void SetPreference(AppThemePreference preference)
    {
        _preference = preference;
        ApplyPreference();
    }

    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        ThemeChanged?.Invoke(this, theme);
    }

    public void ApplySystemTheme() => ApplyTheme(GetSystemTheme());

    private void ApplyPreference()
    {
        switch (_preference)
        {
            case AppThemePreference.Light:
                ApplyTheme(AppTheme.Light);
                break;
            case AppThemePreference.Dark:
                ApplyTheme(AppTheme.Dark);
                break;
            default:
                ApplySystemTheme();
                break;
        }
    }
}
