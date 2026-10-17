using System.Diagnostics;
using MicaFlyouts.Domain;
using MicaFlyouts.Domain.Flyouts;
using MicaFlyouts.Domain.Media;
using MicaFlyouts.Domain.Settings;
using MicaFlyouts.Infrastructure.Interop;
using MicaFlyouts.Infrastructure.Windows;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Reactor;

namespace MicaFlyouts.UI.Animation;

public static class WindowAnimationCoordinator
{
    private const double SlideDistanceDip = 20;
    private static Animation? _animation;
    private static WindowPosition? _currentPosition;
    private static double _currentOpacity = 1;

    public static void Show(ReactorWindow window, SettingsSnapshot settings, MediaTrackSnapshot? track = null)
    {
        var size = FlyoutWindowDimensions.GetMediaFlyoutSizeDip(settings, track);
        window.SetSize(size.WidthDip, size.HeightDip);
        var (target, _) = GetTarget(settings, size);
        bool reversingHide = ReferenceEquals(_animation?.Window, window) && _animation.HideWhenComplete;

        if (window.IsVisible && !reversingHide)
        {
            if (ReferenceEquals(_animation?.Window, window))
                return;

            window.SetPosition(target.Left, target.Top);
            window.SetOpacity(1);
            _currentPosition = target;
            _currentOpacity = 1;
            ConfigureOverlay(window);
            return;
        }

        StopAnimationAtCurrentPosition();
        int speed = Math.Clamp(settings.FlyoutAnimationSpeed, 0, 5);
        double offset = IsTopPosition(settings.Position) ? -SlideDistanceDip : SlideDistanceDip;
        var start = window.IsVisible
            ? _currentPosition ?? target
            : new WindowPosition(target.Left, target.Top + offset);
        double startOpacity = window.IsVisible ? _currentOpacity : 0;

        if (!window.IsVisible)
        {
            _currentPosition = start;
            _currentOpacity = 0;
            window.SetPosition(start.Left, start.Top);
            window.SetOpacity(0);
            window.Show();
        }

        ConfigureOverlay(window);

        int duration = AnimationDuration(speed);
        if (duration == 0 || !StartAnimation(window, start, target, startOpacity, 1, duration,
                settings.FlyoutAnimationEasingStyle, hideWhenComplete: false))
        {
            window.SetPosition(target.Left, target.Top);
            window.SetOpacity(1);
            _currentPosition = target;
            _currentOpacity = 1;
        }
    }

    public static void Hide(ReactorWindow window, SettingsSnapshot settings, MediaTrackSnapshot? track = null)
    {
        if (!window.IsVisible)
        {
            StopAnimationAtCurrentPosition();
            return;
        }

        var size = FlyoutWindowDimensions.GetMediaFlyoutSizeDip(settings, track);
        window.SetSize(size.WidthDip, size.HeightDip);
        var (target, _) = GetTarget(settings, size);
        StopAnimationAtCurrentPosition();
        int speed = Math.Clamp(settings.FlyoutAnimationSpeed, 0, 5);
        double offset = IsTopPosition(settings.Position) ? -SlideDistanceDip : SlideDistanceDip;
        var start = _currentPosition ?? target;
        var end = new WindowPosition(target.Left, target.Top + offset);
        double startOpacity = _currentOpacity;
        int duration = AnimationDuration(speed);

        if (duration == 0 || !StartAnimation(window, start, end, startOpacity, 0, duration,
                settings.FlyoutAnimationEasingStyle, hideWhenComplete: true))
        {
            window.SetOpacity(0);
            window.Hide();
            _currentPosition = null;
            _currentOpacity = 0;
        }
    }

    private static bool StartAnimation(
        ReactorWindow window,
        WindowPosition start,
        WindowPosition end,
        double fromOpacity,
        double toOpacity,
        int durationMilliseconds,
        int easingStyle,
        bool hideWhenComplete)
    {
        var dispatcher = ReactorApp.UIDispatcher;
        if (dispatcher is null)
            return false;

        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.IsRepeating = true;
        var animation = new Animation(
            window,
            timer,
            start,
            end,
            fromOpacity,
            toOpacity,
            Stopwatch.GetTimestamp(),
            TimeSpan.FromMilliseconds(durationMilliseconds),
            Math.Clamp(easingStyle, 0, 3),
            hideWhenComplete);
        _animation = animation;
        timer.Tick += (_, _) => Advance(animation);
        timer.Start();
        return true;
    }

    private static void Advance(Animation animation)
    {
        if (!ReferenceEquals(_animation, animation))
        {
            animation.Timer.Stop();
            return;
        }

        double progress = Progress(animation);
        Apply(animation, progress);
        if (progress < 1)
            return;

        animation.Timer.Stop();
        _animation = null;
        if (animation.HideWhenComplete)
        {
            animation.Window.Hide();
            _currentPosition = null;
            _currentOpacity = 0;
            return;
        }

        _currentPosition = animation.End;
        _currentOpacity = animation.ToOpacity;
    }

    private static void StopAnimationAtCurrentPosition()
    {
        if (_animation is not { } animation)
            return;

        Apply(animation, Progress(animation));
        animation.Timer.Stop();
        _animation = null;
    }

    private static void Apply(Animation animation, double progress)
    {
        double eased = Ease(progress, animation.EasingStyle, animation.HideWhenComplete);
        var position = new WindowPosition(
            Lerp(animation.Start.Left, animation.End.Left, eased),
            Lerp(animation.Start.Top, animation.End.Top, eased));
        double opacity = Lerp(animation.FromOpacity, animation.ToOpacity, eased);
        animation.Window.SetPosition(position.Left, position.Top);
        animation.Window.SetOpacity(opacity);
        _currentPosition = position;
        _currentOpacity = opacity;
    }

    private static (WindowPosition Position, double Scale) GetTarget(
        SettingsSnapshot settings,
        (double WidthDip, double HeightDip) size)
    {
        var monitor = MonitorService.Select(settings.FlyoutSelectedMonitor);
        double scale = monitor.DpiScale;
        var windowSize = new ScreenRect(
            0,
            0,
            size.WidthDip * scale,
            size.HeightDip * scale);
        var target = FlyoutPositionCalculator.Calculate(
            (FlyoutPosition)Math.Clamp(settings.Position, 0, 5),
            windowSize,
            monitor.WorkArea,
            settings.VolumeControlEnabled,
            settings.VolumeControlAboveMediaFlyout);
        return (new WindowPosition(target.Left / scale, target.Top / scale), scale);
    }

    private static void ConfigureOverlay(ReactorWindow window)
        => NativeWindowApi.ConfigureOverlayWindow(
            WinRT.Interop.WindowNative.GetWindowHandle(window.NativeWindow),
            topmost: true,
            noActivate: true);

    private static int AnimationDuration(int speed)
        => speed switch
        {
            0 => 0,
            1 => 150,
            2 => 300,
            3 => 450,
            4 => 600,
            _ => 900,
        };

    private static double Ease(double progress, int style, bool easeOut)
    {
        progress = Math.Clamp(progress, 0, 1);
        if (style == 0)
            return progress;

        double value = style switch
        {
            1 => 1 - Math.Cos(progress * Math.PI / 2),
            2 => progress * progress,
            _ => progress * progress * progress,
        };
        return easeOut ? 1 - Ease(1 - progress, style, false) : value;
    }

    private static double Progress(Animation animation)
    {
        long elapsed = Stopwatch.GetTimestamp() - animation.StartedAt;
        double seconds = (double)elapsed / Stopwatch.Frequency;
        return Math.Clamp(seconds / animation.Duration.TotalSeconds, 0, 1);
    }

    private static bool IsTopPosition(int position)
        => position is (int)FlyoutPosition.TopLeft or (int)FlyoutPosition.TopCenter or (int)FlyoutPosition.TopRight;

    private static double Lerp(double from, double to, double progress)
        => from + (to - from) * progress;

    private sealed record Animation(
        ReactorWindow Window,
        DispatcherQueueTimer Timer,
        WindowPosition Start,
        WindowPosition End,
        double FromOpacity,
        double ToOpacity,
        long StartedAt,
        TimeSpan Duration,
        int EasingStyle,
        bool HideWhenComplete);
}
