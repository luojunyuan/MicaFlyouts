using System.Globalization;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Infrastructure.Settings;
using MicaFlyouts.Infrastructure.State;
using Microsoft.UI.Reactor.Localization;

namespace MicaFlyouts.Infrastructure.Localization;

public sealed class LocalizationService
{
    private readonly ISettingsStore _settings;
    private readonly LocalizationStore _store;
    private readonly ReswResourceProvider _provider;

    public LocalizationService(ISettingsStore settings, LocalizationStore store, string? stringsPath = null)
    {
        _settings = settings;
        _store = store;
        _provider = stringsPath is null
            ? new ReswResourceProvider()
            : new ReswResourceProvider("en-US", stringsPath);
        Apply();
    }

    public LocalizationSnapshot Snapshot => _store.Snapshot;

    public IStringResourceProvider ResourceProvider => _provider;

    public void Apply()
    {
        string systemLanguage = CultureInfo.CurrentUICulture.Name;
        string language = LocalizationCatalog.Resolve(_settings.Snapshot.AppLanguage, systemLanguage);
        _store.Set(new LocalizationSnapshot(
            language,
            LocalizationCatalog.IsRightToLeft(language),
            string.IsNullOrWhiteSpace(_settings.Snapshot.FontFamily)
                ? LocalizationCatalog.FontFamilyFor(language)
                : _settings.Snapshot.FontFamily,
            _store.Snapshot.ResourceVersion + 1));
    }

    public string Get(string key, string fallback)
    {
        try
        {
            var value = _provider.GetString(Snapshot.Language, "App", key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch
        {
            return fallback;
        }
    }
}
