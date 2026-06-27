using System.Globalization;

namespace HyoPDF.Core.Localization;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    event EventHandler? CultureChanged;
    void SetCulture(string cultureName);
    string GetString(string key);
}
