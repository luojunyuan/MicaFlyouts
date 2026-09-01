using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using System.Runtime.InteropServices.WindowsRuntime;
using static Microsoft.UI.Reactor.Core.Theme;
using static Microsoft.UI.Reactor.Factories;

namespace MicaFlyouts.UI.Components;

public sealed record CoverImageProps(byte[]? Bytes, double Size = 78);

public sealed class CoverImage : Component<CoverImageProps>
{
    public override Element Render()
    {
        var (bitmap, setBitmap) = UseState<BitmapImage?>(null);
        UseEffect(() =>
        {
            var cancellation = new CancellationTokenSource();
            _ = LoadAsync(Props.Bytes, setBitmap, cancellation.Token);
            return () => cancellation.Cancel();
        }, Props.Bytes is null || Props.Bytes.Length == 0 ? 0 : HashCode.Combine(Props.Bytes.Length, Props.Bytes[0]));

        Element content = bitmap is null
            ? Icon(SymbolIcon("MusicInfo"))
            : new XamlHostElement(
                () => new Image { Stretch = Stretch.UniformToFill },
                control => ((Image)control).Source = bitmap);
        return Border(content)
            .Width(Props.Size)
            .Height(Props.Size)
            .CornerRadius(6)
            .WithBorder(ControlStroke, 1);
    }

    private static async Task LoadAsync(byte[]? bytes, Action<BitmapImage?> setBitmap, CancellationToken cancellationToken)
    {
        if (bytes is null || bytes.Length == 0)
        {
            setBitmap(null);
            return;
        }
        try
        {
            var image = new BitmapImage();
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            await image.SetSourceAsync(stream);
            cancellationToken.ThrowIfCancellationRequested();
            setBitmap(image);
        }
        catch (OperationCanceledException) { }
        catch { setBitmap(null); }
    }
}

public static class TextComponents
{
    public static TextBlockElement MarqueeText(string text)
        => TextBlock(text ?? string.Empty)
            .TextTrimming(TextTrimming.CharacterEllipsis)
            .TextWrapping(TextWrapping.NoWrap);

    public static TextBlockElement ScrollText(string text)
        => MarqueeText(text);
}
