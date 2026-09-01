using MicaFlyouts.Domain;
using MicaFlyouts.Domain.Localization;
using MicaFlyouts.Domain.LockKeys;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Onboarding;
using MicaFlyouts.Domain.Taskbar;
using MicaFlyouts.Domain.Updates;
using MicaFlyouts.Domain.Visualizer;
using MicaFlyouts.Domain.Volume;

namespace MicaFlyouts.Infrastructure.State;

public abstract class SnapshotStore<TSnapshot> : IStateStore<TSnapshot>, IDisposable
{
    private readonly StateStore<TSnapshot> _state;

    protected SnapshotStore(TSnapshot initialSnapshot)
    {
        _state = new StateStore<TSnapshot>(initialSnapshot);
    }

    public TSnapshot Snapshot => _state.Snapshot;

    public Action Subscribe(Action listener) => _state.Subscribe(listener);

    public void Set(TSnapshot snapshot) => _state.SetSnapshot(snapshot);

    public TSnapshot Update(Func<TSnapshot, TSnapshot> reducer) => _state.Update(reducer);

    public void Dispose()
    {
        _state.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class MediaStore : SnapshotStore<MediaSnapshot>
{
    public MediaStore() : base(MediaSnapshot.Empty) { }
}

public sealed class VolumeStore : SnapshotStore<VolumeSnapshot>
{
    public VolumeStore() : base(VolumeSnapshot.Empty) { }
}

public sealed class LockKeyStore : SnapshotStore<LockKeySnapshot>
{
    public LockKeyStore() : base(new LockKeySnapshot(false, false, false, false)) { }
}

public sealed class TaskbarStore : SnapshotStore<TaskbarSnapshot>
{
    public TaskbarStore() : base(new TaskbarSnapshot(null, TaskbarOrientation.Horizontal, PixelRect.Empty, false, false, "-", "-", false, false)) { }
}

public sealed class VisualizerStore : SnapshotStore<VisualizerSnapshot>
{
    public VisualizerStore() : base(VisualizerSnapshot.Empty) { }
}

public sealed class OnboardingStore : SnapshotStore<OnboardingSnapshot>
{
    public OnboardingStore() : base(OnboardingSnapshot.Initial) { }
}

public sealed class LocalizationStore : SnapshotStore<LocalizationSnapshot>
{
    public LocalizationStore(string language, int resourceVersion = 1)
        : base(new LocalizationSnapshot(
            language,
            LocalizationCatalog.IsRightToLeft(language),
            LocalizationCatalog.FontFamilyFor(language),
            resourceVersion))
    {
    }
}

public sealed class UpdateStore : SnapshotStore<UpdateSnapshot>
{
    public UpdateStore() : base(UpdateSnapshot.Empty) { }
}
