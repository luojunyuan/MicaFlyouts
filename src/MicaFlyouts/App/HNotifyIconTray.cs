using Icon = System.Drawing.Icon;
using System.Windows.Input;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Hosting;
using Microsoft.UI.Reactor.Wrappers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using static MicaFlyouts.App.TaskbarIconElement;
using MUXC = Microsoft.UI.Xaml.Controls;

namespace MicaFlyouts.App;

/// <summary>
/// Reactor's generated element bridge for H.NotifyIcon. It lives beside the
/// public tray facade so consumers do not need a separate generator file.
/// </summary>
[GenerateReactorWrapper(
    typeof(H.NotifyIcon.TaskbarIcon),
    AutoDiscover = false,
    Include = new[]
    {
        "Icon",
        "ToolTipText",
        "MenuActivation",
        "PopupActivation",
    })]
[WrapLifecycle(nameof(CreateTrayIcon), OnUnmounted = nameof(DisposeTrayIcon))]
public partial record TaskbarIconElement
{
    private static void CreateTrayIcon(H.NotifyIcon.TaskbarIcon taskbarIcon) =>
        taskbarIcon.ForceCreate(enablesEfficiencyMode: false);

    private static void DisposeTrayIcon(H.NotifyIcon.TaskbarIcon taskbarIcon) =>
        taskbarIcon.Dispose();
}

/// <summary>Configuration for an H.NotifyIcon tray surface.</summary>
public sealed record HNotifyIconSpec(HNotifyIcon Icon, string Tooltip, params MenuFlyoutItemBase[] MenuItems)
{
    public FlowDirection FlowDirection { get; init; }
    public string? FontFamily { get; init; }
}

/// <summary>H.NotifyIcon icon source owned by the tray adapter.</summary>
public sealed class HNotifyIcon
{
    private HNotifyIcon(Icon icon) => DrawingIcon = icon;

    internal Icon DrawingIcon { get; }

    /// <summary>Creates an H.NotifyIcon source from a caller-provided icon.</summary>
    public static HNotifyIcon FromDrawingIcon(Icon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return new HNotifyIcon((Icon)icon.Clone());
    }
}

/// <summary>H.NotifyIcon equivalent of ReactorApp for this optional adapter.</summary>
public static class HNotifyIconApp
{
    public static HNotifyIconTray OpenTrayIcon(HNotifyIconSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return HNotifyIconTray.Create(spec);
    }
}

/// <summary>
/// Generic H.NotifyIcon adapter backed by a ReactorHostControl.
/// The caller owns the menu content; the returned handle keeps the tray
/// surface alive for the lifetime of the application.
/// </summary>
public sealed partial class HNotifyIconTray : IDisposable
{
    private readonly ReactorHostControl _host;
    private HNotifyIcon _iconSource;
    private MUXC.MenuFlyout _contextMenu;
    private H.NotifyIcon.TaskbarIcon? _taskbarIcon;
    private int _disposed;

    /// <summary>Fires when the user left-clicks the tray icon.</summary>
    public event EventHandler? LeftClick;

    private HNotifyIconTray(
        HNotifyIcon iconSource,
        string tooltip,
        MUXC.MenuFlyout contextMenu,
        FlowDirection flowDirection)
    {
        _iconSource = iconSource;
        _contextMenu = contextMenu;
        _host = new ReactorHostControl();
        _host.Mount(_ => TaskbarIcon(
            icon: _iconSource.DrawingIcon,
            toolTipText: tooltip,
            menuActivation: H.NotifyIcon.Core.PopupActivationMode.RightClick,
            popupActivation: H.NotifyIcon.Core.PopupActivationMode.None)
            .Set(taskbarIcon =>
            {
                _taskbarIcon = taskbarIcon;
                taskbarIcon.FlowDirection = flowDirection;
                taskbarIcon.ContextMenuMode = H.NotifyIcon.ContextMenuMode.SecondWindow;
                taskbarIcon.ContextFlyout = _contextMenu;
                taskbarIcon.CloseContextMenuOnItemClick = true;
                taskbarIcon.NoLeftClickDelay = true;
                taskbarIcon.LeftClickCommand = new HNotifyIconCommand(RaiseLeftClick);
            }));
    }

    internal static HNotifyIconTray Create(HNotifyIconSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec.Icon);
        ArgumentException.ThrowIfNullOrEmpty(spec.Tooltip);

        return new HNotifyIconTray(
            spec.Icon,
            spec.Tooltip,
            CreateContextMenu(spec.MenuItems, spec.FlowDirection, spec.FontFamily),
            spec.FlowDirection);
    }

    public void UpdateIcon(HNotifyIcon icon)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        ArgumentNullException.ThrowIfNull(icon);
        if (ReferenceEquals(_iconSource, icon))
            return;
        if (_taskbarIcon is not null)
            _taskbarIcon.Icon = icon.DrawingIcon;
        else
            _iconSource.DrawingIcon.Dispose();
        _iconSource = icon;
    }

    private static MUXC.MenuFlyout CreateContextMenu(
        IReadOnlyList<MenuFlyoutItemBase> items,
        FlowDirection flowDirection,
        string? fontFamily)
    {
        var menu = new MUXC.MenuFlyout
        {
            MenuFlyoutPresenterStyle = CreateMenuPresenterStyle(flowDirection, fontFamily),
        };
        foreach (var item in items)
            menu.Items.Add(CreateMenuItem(item));
        return menu;
    }

    private static Style CreateMenuPresenterStyle(FlowDirection flowDirection, string? fontFamily)
    {
        var style = new Style { TargetType = typeof(MUXC.MenuFlyoutPresenter) };
        style.Setters.Add(new Setter(FrameworkElement.FlowDirectionProperty, flowDirection));
        if (!string.IsNullOrWhiteSpace(fontFamily))
            style.Setters.Add(new Setter(MUXC.Control.FontFamilyProperty, new FontFamily(fontFamily)));
        return style;
    }

    private static MUXC.MenuFlyoutItemBase CreateMenuItem(MenuFlyoutItemBase item) => item switch
    {
        MenuFlyoutItemData menuItem => CreateMenuItem(menuItem),
        MenuFlyoutSeparatorData => new MUXC.MenuFlyoutSeparator(),
        MenuFlyoutSubItemData subItem => CreateSubItem(subItem),
        ToggleMenuFlyoutItemData toggleItem => CreateToggleItem(toggleItem),
        RadioMenuFlyoutItemData radioItem => CreateRadioItem(radioItem),
        _ => throw new ArgumentException(
            $"Unsupported Reactor menu item type '{item.GetType().Name}'.",
            nameof(item)),
    };

    private static MUXC.MenuFlyoutItem CreateMenuItem(MenuFlyoutItemData data)
    {
        var item = new MUXC.MenuFlyoutItem
        {
            Text = data.Text,
            IsEnabled = data.IsEnabled,
            AccessKey = data.AccessKey,
            Icon = ResolveIcon(data.Icon),
        };
        item.Click += (_, _) => data.OnClick?.Invoke();
        AddKeyboardAccelerators(item, data.KeyboardAccelerators);
        return item;
    }

    private static MUXC.MenuFlyoutSubItem CreateSubItem(MenuFlyoutSubItemData data)
    {
        var item = new MUXC.MenuFlyoutSubItem
        {
            Text = data.Text,
            Icon = ResolveIcon(data.Icon),
        };
        foreach (var child in data.Items)
            item.Items.Add(CreateMenuItem(child));
        return item;
    }

    private static MUXC.ToggleMenuFlyoutItem CreateToggleItem(ToggleMenuFlyoutItemData data)
    {
        var item = new MUXC.ToggleMenuFlyoutItem
        {
            Text = data.Text,
            IsChecked = data.IsChecked,
            Icon = ResolveIcon(data.Icon),
        };
        item.Click += (_, _) => data.OnIsCheckedChanged?.Invoke(item.IsChecked);
        return item;
    }

    private static MUXC.RadioMenuFlyoutItem CreateRadioItem(RadioMenuFlyoutItemData data)
    {
        var item = new MUXC.RadioMenuFlyoutItem
        {
            Text = data.Text,
            GroupName = data.GroupName,
            IsChecked = data.IsChecked,
            Icon = ResolveIcon(data.Icon),
        };
        item.Click += (_, _) => data.OnClick?.Invoke();
        return item;
    }

    private static void AddKeyboardAccelerators(
        MUXC.MenuFlyoutItem item,
        KeyboardAcceleratorData[]? accelerators)
    {
        if (accelerators is null) return;
        foreach (var accelerator in accelerators)
        {
            item.KeyboardAccelerators.Add(new KeyboardAccelerator
            {
                Key = accelerator.Key,
                Modifiers = accelerator.Modifiers,
            });
        }
    }

    private static MUXC.IconElement? ResolveIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        if (icon.StartsWith("path:", StringComparison.Ordinal))
            return new MUXC.PathIcon { Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), icon[5..]) };
        return Enum.TryParse<MUXC.Symbol>(icon, ignoreCase: true, out var symbol)
            ? new MUXC.SymbolIcon { Symbol = symbol }
            : new MUXC.FontIcon { Glyph = icon };
    }

    private void RaiseLeftClick() =>
        LeftClick?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var taskbarIcon = _taskbarIcon;
        _taskbarIcon = null;
        taskbarIcon?.Dispose();
        _host.Dispose();
    }
}

file sealed partial class HNotifyIconCommand(Action execute) : ICommand
{
    private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
