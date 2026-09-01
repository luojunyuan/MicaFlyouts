using System.Text;
using System.Text.Json;
using MicaFlyouts.Domain.Settings;

namespace MicaFlyouts.Infrastructure.Settings;

public interface ISettingsRepository
{
    SettingsSnapshot Load();
    void Save(SettingsSnapshot settings);
    Task SaveAsync(SettingsSnapshot settings, CancellationToken cancellationToken = default);
    string Export(SettingsSnapshot settings);
    bool TryImport(string json, Guid currentUuid, out SettingsSnapshot settings);
}

public sealed class JsonSettingsRepository : ISettingsRepository
{
    private readonly string _settingsPath;
    private readonly string _backupPath;
    private readonly string _temporaryPath;
    private readonly object _fileGate = new();

    public JsonSettingsRepository(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MicaFlyouts",
            "settings.json");
        _backupPath = _settingsPath + ".bak";
        _temporaryPath = _settingsPath + ".tmp";
    }

    public string SettingsPath => _settingsPath;

    public SettingsSnapshot Load()
    {
        lock (_fileGate)
        {
            if (TryRead(_settingsPath, out var settings))
                return SettingsValidator.Normalize(settings!);
            if (TryRead(_backupPath, out settings))
                return SettingsValidator.Normalize(settings!);
            return SettingsSnapshot.CreateDefault();
        }
    }

    public void Save(SettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = SettingsValidator.Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, SettingsJsonContext.Default.SettingsSnapshot);

        lock (_fileGate)
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_temporaryPath, json, new UTF8Encoding(false));
            try
            {
                if (File.Exists(_settingsPath))
                {
                    File.Replace(_temporaryPath, _settingsPath, _backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(_temporaryPath, _settingsPath, overwrite: true);
                }
            }
            finally
            {
                if (File.Exists(_temporaryPath))
                    File.Delete(_temporaryPath);
            }
        }
    }

    public Task SaveAsync(SettingsSnapshot settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => Save(settings), cancellationToken);
    }

    public string Export(SettingsSnapshot settings)
    {
        var normalized = SettingsValidator.Normalize(settings);
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(normalized, SettingsJsonContext.Default.SettingsSnapshot));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("uuid"))
                    continue;
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public bool TryImport(string json, Guid currentUuid, out SettingsSnapshot settings)
    {
        settings = SettingsSnapshot.CreateDefault(currentUuid);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsSnapshot);
            if (parsed is null)
                return false;
            settings = SettingsValidator.ForImport(SettingsJsonDefaults.ApplyMissing(json, parsed), currentUuid);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryRead(string path, out SettingsSnapshot? settings)
    {
        settings = null;
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var parsed = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsSnapshot);
            settings = parsed is null ? null : SettingsJsonDefaults.ApplyMissing(json, parsed);
            return settings is not null;
        }
        catch (IOException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
