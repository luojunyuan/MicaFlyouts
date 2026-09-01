using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using MicaFlyouts.Domain;
using MicaFlyouts.Infrastructure.Interop;
using Win32 = Windows.Win32;
using Win32Com = Windows.Win32.System.Com;

namespace MicaFlyouts.Infrastructure.Windows;

/// <summary>
/// A small AOT-safe UI Automation client used only for taskbar child bounds.
/// The generated COM projection is intentionally limited to the vtable methods
/// needed by this service.
/// </summary>
internal static partial class TaskbarAutomationService
{
    private const int TreeScopeDescendants = 4;
    private const int AutomationIdPropertyId = 30011;
    private const int BoundingRectanglePropertyId = 30001;
    private static readonly Guid CUiAutomationClassId = new("ff48dba4-60ef-4201-aa87-54103eef594e");

    public static bool TryGetRect(nint taskbar, string automationId, out PixelRect rect)
    {
        rect = PixelRect.Empty;
        if (taskbar == 0 || string.IsNullOrWhiteSpace(automationId))
            return false;

        try
        {
            if (!NativeWindowApi.TryCreateUiAutomation(CUiAutomationClassId, out IUiAutomation? automation)
                || automation is null)
                return false;
            if (automation.ElementFromHandle(taskbar, out var root) < 0 || root is null)
                return false;
            if (automation.CreatePropertyCondition(AutomationIdPropertyId, automationId, out var condition) < 0
                || condition is null)
                return false;
            if (root.FindFirst(TreeScopeDescendants, condition, out var element) < 0 || element is null)
                return false;
            if (element.GetCurrentPropertyValue(BoundingRectanglePropertyId, out var value) < 0
                || value is not Array bounds
                || bounds.Length < 4)
                return false;
            int left = Convert.ToInt32(bounds.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
            int top = Convert.ToInt32(bounds.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
            int right = Convert.ToInt32(bounds.GetValue(2), System.Globalization.CultureInfo.InvariantCulture);
            int bottom = Convert.ToInt32(bounds.GetValue(3), System.Globalization.CultureInfo.InvariantCulture);
            rect = new PixelRect(left, top, right - left, bottom - top);
            return !rect.IsEmpty;
        }
        catch
        {
            return false;
        }
    }

    [GeneratedComInterface]
    [Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee")]
    internal partial interface IUiAutomation
    {
        [PreserveSig] int CompareElements(nint a, nint b, out int areSame);
        [PreserveSig] int CompareRuntimeIds(nint a, nint b, out nint areSame);
        [PreserveSig] int GetRootElement(out IUiAutomationElement root);
        [PreserveSig] int ElementFromHandle(nint hwnd, out IUiAutomationElement element);
        [PreserveSig] int ElementFromPoint(UiPoint point, out IUiAutomationElement element);
        [PreserveSig] int GetFocusedElement(out IUiAutomationElement element);
        [PreserveSig] int GetRootElementBuildCache(nint cacheRequest, out IUiAutomationElement element);
        [PreserveSig] int ElementFromHandleBuildCache(nint hwnd, nint cacheRequest, out IUiAutomationElement element);
        [PreserveSig] int ElementFromPointBuildCache(UiPoint point, nint cacheRequest, out IUiAutomationElement element);
        [PreserveSig] int GetFocusedElementBuildCache(nint cacheRequest, out IUiAutomationElement element);
        [PreserveSig] int CreateTreeWalker(nint condition, out nint walker);
        [PreserveSig] int GetControlViewWalker(out nint walker);
        [PreserveSig] int GetContentViewWalker(out nint walker);
        [PreserveSig] int GetRawViewWalker(out nint walker);
        [PreserveSig] int GetRawViewCondition(out nint condition);
        [PreserveSig] int GetControlViewCondition(out nint condition);
        [PreserveSig] int GetContentViewCondition(out nint condition);
        [PreserveSig] int CreateCacheRequest(out nint cacheRequest);
        [PreserveSig] int CreateTrueCondition(out nint condition);
        [PreserveSig] int CreateFalseCondition(out nint condition);
        [PreserveSig] int CreatePropertyCondition(
            int propertyId,
            [MarshalAs(UnmanagedType.Struct)] object value,
            out IUiAutomationCondition condition);
    }

    [GeneratedComInterface]
    [Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e")]
    internal partial interface IUiAutomationElement
    {
        [PreserveSig] int SetFocus();
        [PreserveSig] int GetRuntimeId(out nint runtimeId);
        [PreserveSig] int FindFirst(int scope, IUiAutomationCondition condition, out IUiAutomationElement element);
        [PreserveSig] int FindAll(int scope, IUiAutomationCondition condition, out nint found);
        [PreserveSig] int FindFirstBuildCache(int scope, nint condition, nint cacheRequest, out IUiAutomationElement element);
        [PreserveSig] int FindAllBuildCache(int scope, nint condition, nint cacheRequest, out nint elements);
        [PreserveSig] int BuildUpdatedCache(nint cacheRequest, out IUiAutomationElement updated);
        [PreserveSig] int GetCurrentPropertyValue(
            int propertyId,
            [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCurrentPropertyValueEx(
            int propertyId,
            [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue,
            [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCachedPropertyValue(
            int propertyId,
            [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCachedPropertyValueEx(
            int propertyId,
            [MarshalAs(UnmanagedType.Bool)] bool ignoreDefaultValue,
            [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCurrentPattern(int patternId, out nint patternObject);
        [PreserveSig] int GetCachedPattern(int patternId, out nint patternObject);
        [PreserveSig] int GetCachedParent(out IUiAutomationElement parent);
        [PreserveSig] int GetCachedChildren(out nint children);
        [PreserveSig] int GetCurrentProcessId(out int processId);
        [PreserveSig] int GetCurrentControlType(out int controlType);
        [PreserveSig] int GetCurrentLocalizedControlType([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentName([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentAcceleratorKey([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentAccessKey([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentHasKeyboardFocus([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentIsKeyboardFocusable([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentIsEnabled([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentAutomationId([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentClassName([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentHelpText([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentCulture(out int value);
        [PreserveSig] int GetCurrentIsControlElement([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentIsContentElement([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentIsPassword([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentNativeWindowHandle(out nint value);
        [PreserveSig] int GetCurrentItemType([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentIsOffscreen([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentOrientation(out int value);
        [PreserveSig] int GetCurrentFrameworkId([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentIsRequiredForForm([MarshalAs(UnmanagedType.Bool)] out bool value);
        [PreserveSig] int GetCurrentItemStatus([MarshalAs(UnmanagedType.BStr)] out string value);
        [PreserveSig] int GetCurrentBoundingRectangle(out UiRect value);
    }

    [GeneratedComInterface]
    [Guid("352ffba8-0973-437c-a61f-f64cafd81df9")]
    internal partial interface IUiAutomationCondition
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UiPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct UiRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
