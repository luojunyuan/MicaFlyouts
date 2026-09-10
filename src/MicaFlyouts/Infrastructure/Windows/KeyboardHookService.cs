using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MicaFlyouts.Domain.LockKeys;
using Win32 = Windows.Win32;
using Win32Foundation = Windows.Win32.Foundation;
using Win32Messaging = Windows.Win32.UI.WindowsAndMessaging;

namespace MicaFlyouts.Infrastructure.Windows;

public sealed unsafe partial class KeyboardHookService : IDisposable
{
    public const int CapsLock = 0x14;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;
    public const int Insert = 0x2D;
    public const int VolumeMute = 0xAD;
    public const int VolumeDown = 0xAE;
    public const int VolumeUp = 0xAF;
    public const int MediaNext = 0xB0;
    public const int MediaPrevious = 0xB1;
    public const int MediaStop = 0xB2;
    public const int MediaPlayPause = 0xB3;

    private const nuint KeyDown = 0x0100;
    private const nuint KeyUp = 0x0101;
    private const nuint SysKeyDown = 0x0104;
    private const nuint SysKeyUp = 0x0105;

    private static readonly object CallbackGate = new();
    private static KeyboardHookService? _current;
    private readonly Action<KeyboardKeyEvent> _keyReceived;
    private Win32.FreeLibrarySafeHandle? _module;
    private Win32.UnhookWindowsHookExSafeHandle? _hook;
    private int _started;
    private int _disposed;

    public KeyboardHookService(Action<KeyboardKeyEvent> keyReceived)
    {
        _keyReceived = keyReceived ?? throw new ArgumentNullException(nameof(keyReceived));
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0 || Volatile.Read(ref _disposed) != 0)
            return;

        lock (CallbackGate)
        {
            if (Volatile.Read(ref _current) is not null)
            {
                Volatile.Write(ref _started, 0);
                return;
            }

            // lParam is a pointer to KBDLLHOOKSTRUCT. The static reference keeps
            // this service rooted for the lifetime of the native hook callback.
            Volatile.Write(ref _current, this);
            try
            {
                _module = Win32.PInvoke.GetModuleHandle(null);
                _hook = Win32.PInvoke.SetWindowsHookEx(
                    Win32Messaging.WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
                    &HookCallback,
                    _module,
                    0);
                if (_hook is null || _hook.IsInvalid)
                {
                    _hook?.Dispose();
                    _hook = null;
                    _module?.Dispose();
                    _module = null;
                    Volatile.Write(ref _current, null);
                    Volatile.Write(ref _started, 0);
                }
            }
            catch
            {
                _hook?.Dispose();
                _hook = null;
                _module?.Dispose();
                _module = null;
                Volatile.Write(ref _current, null);
                Volatile.Write(ref _started, 0);
                throw;
            }
        }
    }

    public static LockKeySnapshot ReadSnapshot()
        => new(
            IsOn(CapsLock),
            IsOn(NumLock),
            IsOn(ScrollLock),
            IsOn(Insert));

    private static bool IsOn(int key)
        => (Win32.PInvoke.GetKeyState(key) & 1) != 0;

    private void OnKeyboardEvent(int virtualKey, bool isKeyUp)
    {
        if (virtualKey is not (
            CapsLock or NumLock or ScrollLock or Insert
            or VolumeMute or VolumeDown or VolumeUp
            or MediaNext or MediaPrevious or MediaStop or MediaPlayPause))
            return;
        try
        {
            _keyReceived(new KeyboardKeyEvent(virtualKey, isKeyUp, Environment.TickCount64));
        }
        catch
        {
            // Hook callbacks must never unwind into User32.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static Win32Foundation.LRESULT HookCallback(
        int code,
        Win32Foundation.WPARAM wParam,
        Win32Foundation.LPARAM lParam)
    {
        if (code >= 0 && (nuint)wParam is KeyDown or KeyUp or SysKeyDown or SysKeyUp)
        {
            try
            {
                var service = Volatile.Read(ref _current);
                if (service is null || Volatile.Read(ref service._disposed) != 0 || (nint)lParam == 0)
                    return Win32.PInvoke.CallNextHookEx(null, code, wParam, lParam);

                var data = Unsafe.ReadUnaligned<Win32Messaging.KBDLLHOOKSTRUCT>((void*)(nint)lParam);
                service.OnKeyboardEvent((int)data.vkCode, (nuint)wParam is KeyUp or SysKeyUp);
            }
            catch
            {
                // A stale callback is ignored while the hook is being removed.
            }
        }

        return Win32.PInvoke.CallNextHookEx(null, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        lock (CallbackGate)
        {
            _hook?.Dispose();
            _hook = null;
            _module?.Dispose();
            _module = null;
            if (ReferenceEquals(Volatile.Read(ref _current), this))
                Volatile.Write(ref _current, null);
        }
    }
}
