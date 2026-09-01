namespace MicaFlyouts.Domain.Localization;

public sealed record LocalizationSnapshot(
    string Language,
    bool IsRightToLeft,
    string FontFamily,
    int ResourceVersion);

public static class LocalizationCatalog
{
    public static IReadOnlyList<string> SupportedLanguages { get; } =
    [
        "ar", "ca", "cs", "de", "en-US", "es", "fi", "fr", "he", "hi", "hr", "hu", "id",
        "it", "ja", "ko", "nl", "pl", "pt-BR", "ru", "si", "sk", "ta", "th", "tr", "uk",
        "vi", "zh-CN", "zh-TW",
    ];

    public static bool IsRightToLeft(string? language) => language is "ar" or "he";

    public static string Resolve(string? requested, string systemLanguage)
    {
        if (string.IsNullOrWhiteSpace(requested) || requested.Equals("system", StringComparison.OrdinalIgnoreCase))
            requested = systemLanguage;
        if (SupportedLanguages.Contains(requested, StringComparer.OrdinalIgnoreCase))
            return SupportedLanguages.First(language => string.Equals(language, requested, StringComparison.OrdinalIgnoreCase));
        return "en-US";
    }

    public static string FontFamilyFor(string? language) => IsRightToLeft(language)
        ? "Segoe UI Variable, Segoe UI, Arial"
        : language switch
        {
            "zh-CN" or "zh-TW" => "Segoe UI Variable, Microsoft YaHei UI, Yu Gothic UI",
            "ja" => "Segoe UI Variable, Yu Gothic UI, Meiryo",
            "ko" => "Segoe UI Variable, Malgun Gothic",
            _ => "Segoe UI Variable, Segoe UI",
        };
}
