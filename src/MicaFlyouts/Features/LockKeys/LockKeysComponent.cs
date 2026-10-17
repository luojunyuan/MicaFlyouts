using MicaFlyouts.App;
using MicaFlyouts.Domain.LockKeys;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.UI.Components;
using Microsoft.UI.Xaml;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Reactor.Localization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using VectorPath = Microsoft.UI.Xaml.Shapes.Path;
using static Microsoft.UI.Reactor.Factories;
using static Microsoft.UI.Reactor.Core.Theme;

namespace MicaFlyouts.Features.LockKeys;

public sealed class LockKeysWindowComponent : LocalizedWindowComponent
{
    public override Element Render() => UseLocalized(Component<LockKeysComponent>());
}

public sealed class LockKeysComponent : Component
{
    public override Element Render()
    {
        var services = AppRuntime.Services;
        var t = UseIntl();
        var snapshot = UseExternalStore(services.LockKeyStore.Subscribe, () => services.LockKeyStore.Snapshot);
        var settings = UseExternalStore(services.Settings.Subscribe, () => services.Settings.Snapshot);
        var activeKey = snapshot.ActiveKey;
        if (activeKey is null)
            return Empty();

        var key = activeKey.Value;
        var isOn = key == LockKeyKind.Insert || snapshot[key];
        var label = key switch
        {
            LockKeyKind.CapsLock => StatusText(t.Message(Loc.App.LockWindow_CapsLock), isOn, t),
            LockKeyKind.NumLock => StatusText(t.Message(Loc.App.LockWindow_NumLock), isOn, t),
            LockKeyKind.ScrollLock => StatusText(t.Message(Loc.App.LockWindow_ScrollLock), isOn, t),
            LockKeyKind.Insert => t.Message(Loc.App.LockWindow_InsertPressed),
            _ => string.Empty,
        };

        var status = TextBlock(label)
            .FontSize(14)
            .TextTrimming(TextTrimming.CharacterEllipsis)
            .Flex(grow: 1, basis: 0);
        if (settings.LockKeysBoldUi)
            status = status.SemiBold();

        int animationDuration = settings.FlyoutAnimationSpeed switch
        {
            0 => 0,
            1 => 100,
            2 => 200,
            3 => 300,
            4 => 400,
            _ => 600,
        };

        return Component<FlyoutSurface, FlyoutSurfaceProps>(new FlyoutSurfaceProps(
            Child: VStack(2,
                HStack(8,
                    new XamlHostElement(
                        static () => new LockKeyGlyphView(),
                        control => ((LockKeyGlyphView)control).Update(
                            LockKeyLayout.VisualState(isOn),
                            settings.LockKeysAnimated,
                            animationDuration))
                        .Width(22)
                        .Height(22),
                    status)
                    .Height(25)
                    .VAlign(VerticalAlignment.Center),
                new XamlHostElement(
                    static () => new LockKeyIndicatorView(),
                    control => ((LockKeyIndicatorView)control).Update(
                        LockKeyLayout.VisualState(isOn),
                        settings.LockKeysAnimated,
                        animationDuration))
                    .Width(60)
                    .Height(3)
                    .HorizontalAlignment(HorizontalAlignment.Center)),
            Radius: 8,
            Padding: 8));
    }

    private static string StatusText(string key, bool isOn, IntlAccessor t)
        => $"{key} {t.Message(isOn ? Loc.App.LockWindow_LockOn : Loc.App.LockWindow_LockOff)}";
}

internal sealed partial class LockKeyIndicatorView : Grid
{
    private readonly Border _indicator;
    private Storyboard? _storyboard;
    private double _width = 60;
    private double _opacity = 1;
    private bool _hasState;

    public LockKeyIndicatorView()
    {
        Width = 60;
        Height = 3;
        _indicator = new Border
        {
            Width = _width,
            Height = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(2),
            Opacity = _opacity,
            Background = ResolveAccentBrush(),
        };
        Children.Add(_indicator);
    }

    public void Update(LockKeyVisualState state, bool animate, int durationMilliseconds)
    {
        _storyboard?.Stop();
        _storyboard = null;

        if (!animate || durationMilliseconds <= 0 || !_hasState)
        {
            _width = state.IndicatorWidth;
            _opacity = state.Opacity;
            _indicator.Width = _width;
            _indicator.Opacity = _opacity;
            _hasState = true;
            return;
        }

        double fromWidth = _width;
        double fromOpacity = _opacity;
        _indicator.Width = state.IndicatorWidth;
        _indicator.Opacity = state.Opacity;
        var storyboard = new Storyboard();
        var widthAnimation = new DoubleAnimation
        {
            From = fromWidth,
            To = state.IndicatorWidth,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(widthAnimation, _indicator);
        Storyboard.SetTargetProperty(widthAnimation, "Width");
        storyboard.Children.Add(widthAnimation);

        var opacityAnimation = new DoubleAnimation
        {
            From = fromOpacity,
            To = state.Opacity,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(opacityAnimation, _indicator);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);
        storyboard.Begin();

        _storyboard = storyboard;
        _width = state.IndicatorWidth;
        _opacity = state.Opacity;
        _hasState = true;
    }

    private static Microsoft.UI.Xaml.Media.Brush ResolveAccentBrush()
        => Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var value)
            && value is Microsoft.UI.Xaml.Media.Brush brush
                ? brush
                : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
}

internal sealed partial class LockKeyGlyphView : Grid
{
    private readonly RotateTransform _shackleRotation = new();
    private Storyboard? _storyboard;
    private double _angle;
    private bool _hasState;

    public LockKeyGlyphView()
    {
        Width = 22;
        Height = 22;
        var foreground = ResolveForegroundBrush();
        var canvas = new Microsoft.UI.Xaml.Controls.Canvas { Width = 22, Height = 22 };
        var shackleFigure = new PathFigure { StartPoint = new Point(4, 9) };
        shackleFigure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(4, 4.5),
            Point2 = new Point(7.5, 2),
            Point3 = new Point(11, 2),
        });
        shackleFigure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(14.5, 2),
            Point2 = new Point(18, 4.5),
            Point3 = new Point(18, 9),
        });
        var shackle = new VectorPath
        {
            Data = new PathGeometry { Figures = { shackleFigure } },
            Stroke = foreground,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            RenderTransform = _shackleRotation,
        };
        _shackleRotation.CenterX = 17;
        _shackleRotation.CenterY = 9;

        var body = new Border
        {
            Width = 15,
            Height = 12,
            BorderThickness = new Thickness(2),
            BorderBrush = foreground,
            CornerRadius = new CornerRadius(3),
        };
        var keyhole = new Ellipse
        {
            Width = 2,
            Height = 2,
            Fill = foreground,
        };
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(shackle, 0);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(shackle, 0);
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(body, 3.5);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(body, 8.5);
        Microsoft.UI.Xaml.Controls.Canvas.SetLeft(keyhole, 10);
        Microsoft.UI.Xaml.Controls.Canvas.SetTop(keyhole, 12.5);
        canvas.Children.Add(shackle);
        canvas.Children.Add(body);
        canvas.Children.Add(keyhole);
        Children.Add(canvas);
    }

    public void Update(LockKeyVisualState state, bool animate, int durationMilliseconds)
    {
        _storyboard?.Stop();
        _storyboard = null;
        double fromAngle = _angle;
        _angle = state.ShackleAngle;
        _shackleRotation.Angle = _angle;

        if (!animate || durationMilliseconds <= 0 || !_hasState || Math.Abs(fromAngle - _angle) < 0.1)
        {
            _hasState = true;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = fromAngle,
            To = _angle,
            Duration = TimeSpan.FromMilliseconds(durationMilliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, _shackleRotation);
        Storyboard.SetTargetProperty(animation, "Angle");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _storyboard = storyboard;
        _hasState = true;
    }

    private static Brush ResolveForegroundBrush()
        => Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out var value)
            && value is Brush brush
                ? brush
                : new SolidColorBrush(Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? Microsoft.UI.Colors.White
                    : Microsoft.UI.Colors.Black);
}
