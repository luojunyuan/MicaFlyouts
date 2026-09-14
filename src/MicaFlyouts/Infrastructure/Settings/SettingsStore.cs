using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Infrastructure.Logging;
using MicaFlyouts.Infrastructure.State;

namespace MicaFlyouts.Infrastructure.Settings;

public sealed partial class SettingsStore(ISettingsRepository repository, AppLogger? logger = null) : ISettingsStore, IDisposable
{
    private readonly StateStore<SettingsSnapshot> _state = new(repository.Load());
    private readonly ISettingsRepository _repository = repository;
    private readonly AppLogger? _logger = logger;
    private CancellationTokenSource? _saveCancellation;
    private int _disposed;

    public SettingsSnapshot Snapshot => _state.Snapshot;

    public Action Subscribe(Action listener) => _state.Subscribe(listener);

    public void Update(Func<SettingsSnapshot, SettingsSnapshot> reducer, bool save = true)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _state.Update(current => SettingsValidator.Normalize(reducer(current)));
        if (save)
            ScheduleSave();
    }

    public void Replace(SettingsSnapshot settings, bool save = true)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        _state.SetSnapshot(SettingsValidator.Normalize(settings));
        if (save)
            ScheduleSave();
    }

    public string Export() => _repository.Export(Snapshot);

    public bool TryImport(string json)
    {
        if (!_repository.TryImport(json, Snapshot.Uuid, out var settings))
            return false;
        Replace(settings);
        return true;
    }

    public void SaveNow()
    {
        var cancellation = Interlocked.Exchange(ref _saveCancellation, null);
        cancellation?.Cancel();
        cancellation?.Dispose();
        _repository.Save(Snapshot);
    }

    private void ScheduleSave()
    {
        var next = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _saveCancellation, next);
        previous?.Cancel();
        previous?.Dispose();
        _ = SaveAfterDelayAsync(next);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource source)
    {
        try
        {
            await Task.Delay(500, source.Token).ConfigureAwait(false);
            if (ReferenceEquals(Volatile.Read(ref _saveCancellation), source))
                await _repository.SaveAsync(Snapshot, source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger?.Info("SettingsStore.SaveAfterDelayAsync stopped after OperationCanceledException.");
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _saveCancellation, null, source), source))
                source.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        SaveNow();
        _state.Dispose();
    }
}

public interface ISettingsStore : MicaFlyouts.Domain.IStateStore<SettingsSnapshot>
{
    void Update(Func<SettingsSnapshot, SettingsSnapshot> reducer, bool save = true);
    void Replace(SettingsSnapshot settings, bool save = true);
    string Export();
    bool TryImport(string json);
    void SaveNow();
}
