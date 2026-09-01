namespace MicaFlyouts.Domain;

public interface IStateStore<out TSnapshot>
{
    TSnapshot Snapshot { get; }

    Action Subscribe(Action listener);
}
