using CommunityToolkit.Mvvm.ComponentModel;

using HyoPDF.Core.Localization;

using HyoPDF.Core.Settings;

using HyoPDF.Core.Themes;



namespace HyoPDF.UI.ViewModels;



public partial class SettingsViewModel : ObservableObject

{

    private readonly ILocalSettingsStore _settingsStore;

    private readonly ILocalizationService _localization;

    private readonly IThemeService _themeService;

    private bool _isLoading;



    [ObservableProperty]

    private AppThemePreference _selectedTheme = AppThemePreference.System;



    [ObservableProperty]

    private LanguageOption _selectedLanguageOption = null!;



    [ObservableProperty]

    private int _defaultZoom = 100;



    [ObservableProperty]

    private bool _sidebarVisible = true;



    public AppThemePreference[] ThemeOptions { get; } =

        [AppThemePreference.System, AppThemePreference.Light, AppThemePreference.Dark];



    public LanguageOption[] LanguageOptions { get; } =

    [

        new() { Code = "ko", DisplayName = "한국어" },

        new() { Code = "en", DisplayName = "English" }

    ];



    public event EventHandler<AppSettings>? SettingsChanged;



    public SettingsViewModel(

        ILocalSettingsStore settingsStore,

        ILocalizationService localization,

        IThemeService themeService)

    {

        _settingsStore = settingsStore;

        _localization = localization;

        _themeService = themeService;

        _selectedLanguageOption = LanguageOptions[0];

    }



    public void LoadFromStore()

    {

        _isLoading = true;

        var settings = _settingsStore.Load();

        SelectedTheme = settings.Theme;

        SelectedLanguageOption = FindLanguageOption(settings.Language);

        DefaultZoom = Math.Clamp(settings.DefaultZoom, 25, 500);

        SidebarVisible = settings.SidebarVisible;

        _isLoading = false;



        ApplyTheme(SelectedTheme);

        _localization.SetCulture(SelectedLanguageOption.Code);

    }



    public AppSettings CaptureSettings()

    {

        var current = _settingsStore.Load();

        return new()

        {

            Theme = SelectedTheme,

            Language = SelectedLanguageOption.Code,

            DefaultZoom = Math.Clamp(DefaultZoom, 25, 500),

            SidebarVisible = SidebarVisible,

            ThumbnailSize = current.ThumbnailSize,

            UserResized = current.UserResized,

            LastWindowSize = current.LastWindowSize

        };

    }



    public void SaveWindowSize(double width, double height, bool userResized)

    {

        var settings = _settingsStore.Load();

        settings.UserResized = userResized;

        settings.LastWindowSize = new WindowSize

        {

            Width = Math.Max(800, width),

            Height = Math.Max(560, height)

        };

        _settingsStore.Save(settings);

    }



    private void Persist()

    {

        if (_isLoading)

            return;



        var settings = _settingsStore.Load();

        settings.Theme = SelectedTheme;

        settings.Language = SelectedLanguageOption.Code;

        settings.DefaultZoom = Math.Clamp(DefaultZoom, 25, 500);

        settings.SidebarVisible = SidebarVisible;

        _settingsStore.Save(settings);

        SettingsChanged?.Invoke(this, settings);

    }



    private LanguageOption FindLanguageOption(string language) =>

        LanguageOptions.FirstOrDefault(option => option.Code == language)

        ?? LanguageOptions[0];



    private void ApplyTheme(AppThemePreference preference) => _themeService.SetPreference(preference);



    partial void OnSelectedThemeChanged(AppThemePreference value)

    {

        ApplyTheme(value);

        Persist();

    }



    partial void OnSelectedLanguageOptionChanged(LanguageOption value)

    {

        if (_isLoading)

            return;



        _localization.SetCulture(value.Code);

        Persist();

    }



    partial void OnDefaultZoomChanged(int value) => Persist();



    partial void OnSidebarVisibleChanged(bool value) => Persist();

}

