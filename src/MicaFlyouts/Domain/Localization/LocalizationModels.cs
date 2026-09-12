using System.Globalization;

namespace MicaFlyouts.Domain.Localization;

public sealed record LocalizationSnapshot(
    string Language,
    bool IsRightToLeft,
    string FontFamily,
    int ResourceVersion);

public sealed record LanguageOption(string Value, string DisplayName);

public static class LocalizationCatalog
{
    public const string SystemLanguage = "system";
    public const string DefaultLanguage = "en-US";

    public static IReadOnlyList<string> SupportedLanguages { get; } =
    [
        "ar", "ca", "cs", "de", "en-US", "es", "fi", "fr", "he", "hi", "hr", "hu", "id",
        "it", "ja", "ko", "nl", "pl", "pt-BR", "ru", "si", "sk", "ta", "th", "tr", "uk",
        "vi", "zh-CN", "zh-TW",
    ];

    public static IReadOnlyList<LanguageOption> CreateLanguageOptions(string systemDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemDisplayName);

        return
        [
            new(SystemLanguage, systemDisplayName),
            .. SupportedLanguages.Select(language => new LanguageOption(
                language,
                CultureInfo.GetCultureInfo(language).DisplayName)),
        ];
    }

    public static string NormalizeSelection(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested)
            || string.Equals(requested.Trim(), SystemLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return SystemLanguage;
        }

        return FindSupportedLanguage(requested) ?? DefaultLanguage;
    }

    public static int IndexOfLanguage(IReadOnlyList<LanguageOption> options, string? requested)
    {
        ArgumentNullException.ThrowIfNull(options);

        string normalized = NormalizeSelection(requested);
        int index = -1;
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Value, normalized, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        return index >= 0 ? index : options.Count == 0 ? -1 : 0;
    }

    public static string Resolve(string? requested, string? systemLanguage)
    {
        if (string.IsNullOrWhiteSpace(requested)
            || string.Equals(requested.Trim(), SystemLanguage, StringComparison.OrdinalIgnoreCase))
        {
            requested = systemLanguage;
        }

        return FindSupportedLanguage(requested) ?? DefaultLanguage;
    }

    public static bool IsRightToLeft(string? language)
        => FindSupportedLanguage(language) is "ar" or "he";

    public static string FontFamilyFor(string? language)
    {
        string? canonical = FindSupportedLanguage(language);
        return IsRightToLeft(canonical)
            ? "Segoe UI Variable, Segoe UI, Arial"
            : canonical switch
            {
                "zh-CN" or "zh-TW" => "Segoe UI Variable, Microsoft YaHei UI, Yu Gothic UI",
                "ja" => "Segoe UI Variable, Yu Gothic UI, Meiryo",
                "ko" => "Segoe UI Variable, Malgun Gothic",
                _ => "Segoe UI Variable, Segoe UI",
            };
    }

    private static string? FindSupportedLanguage(string? requested)
    {
        string? candidate = requested?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        string? canonical = CanonicalLanguage(candidate);
        if (canonical is not null)
            return canonical;

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(candidate);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }

        for (CultureInfo? current = culture; current is not null && !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            canonical = CanonicalLanguage(current.Name);
            if (canonical is not null)
                return canonical;

            try
            {
                canonical = CanonicalLanguage(CultureInfo.CreateSpecificCulture(current.Name).Name);
                if (canonical is not null)
                    return canonical;
            }
            catch (CultureNotFoundException)
            {
                // Continue to the parent culture and neutral-language fallback.
            }
        }

        return SupportedLanguages.FirstOrDefault(language =>
            string.Equals(
                CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName,
                culture.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string? CanonicalLanguage(string value)
        => SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language, value, StringComparison.OrdinalIgnoreCase));
}
