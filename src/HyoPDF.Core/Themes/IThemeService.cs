using HyoPDF.Core.Settings;

namespace HyoPDF.Core.Themes;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    AppThemePreference Preference { get; }
    event EventHandler<AppTheme>? ThemeChanged;
    AppTheme GetSystemTheme();
    void SetPreference(AppThemePreference preference);
    void ApplyTheme(AppTheme theme);
    void ApplySystemTheme();
}
