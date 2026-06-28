using System.Globalization;

namespace HyoPDF.UI.Localization;

internal static class LocalizationCulture
{
    public static string NormalizeLanguage(string? language) =>
        language?.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            "ko" => "ko",
            _ => "ko"
        };

    public static CultureInfo ToCultureInfo(string language) =>
        NormalizeLanguage(language) == "en"
            ? CultureInfo.GetCultureInfo("en-US")
            : CultureInfo.GetCultureInfo("ko-KR");
}
