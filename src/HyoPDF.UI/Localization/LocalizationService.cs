using System.Globalization;
using System.Resources;
using HyoPDF.Core.Localization;

namespace HyoPDF.UI.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public LocalizationService()
    {
        _resourceManager = new ResourceManager(
            "HyoPDF.UI.Resources.Strings",
            typeof(LocalizationService).Assembly);
        _currentCulture = new CultureInfo("ko");
    }

    public CultureInfo CurrentCulture => _currentCulture;

    public event EventHandler? CultureChanged;

    public void SetCulture(string cultureName)
    {
        _currentCulture = new CultureInfo(cultureName);
        CultureInfo.CurrentUICulture = _currentCulture;
        CultureInfo.CurrentCulture = _currentCulture;
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key) =>
        _resourceManager.GetString(key, _currentCulture) ?? key;
}
