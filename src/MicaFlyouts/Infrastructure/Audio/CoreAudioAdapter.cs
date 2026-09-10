using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using MicaFlyouts.Domain.Volume;
using MicaFlyouts.Infrastructure.Interop;

namespace MicaFlyouts.Infrastructure.Audio;

/// <summary>
/// Source-generated COM projection for the small Core Audio surface used by the mixer.
/// NAudio remains isolated to the loopback capture adapter.
/// </summary>
internal static class CoreAudioNative
{
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    public static T? Wrap<T>(nint pointer)
        where T : class
    {
        if (pointer == 0)
            return null;

        try
        {
            return ComWrappers.GetOrCreateObjectForComInstance(
                pointer,
                CreateObjectFlags.UniqueInstance) as T;
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    public static T? QueryInterface<T>(nint pointer)
        where T : class
    {
        if (pointer == 0)
            return null;

        Guid interfaceId = typeof(T).GUID;
        int result = Marshal.QueryInterface(pointer, in interfaceId, out nint queried);
        if (result < 0 || queried == 0)
            return null;
        return Wrap<T>(queried);
    }

    public static void Release<T>(T? value)
        where T : class
    {
        if (value is not null && (object)value is ComObject comObject)
            comObject.FinalRelease();
    }

    public static string ReadCoTaskMemString(nint pointer)
    {
        if (pointer == 0)
            return string.Empty;

        try
        {
            return Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    public static void ThrowIfFailed(int result)
    {
        if (result < 0)
            Marshal.ThrowExceptionForHR(result);
    }
}

internal sealed partial class CoreAudioDeviceEnumerator : IDisposable
{
    private const int DataFlowRender = 0;
    private const int RoleMultimedia = 1;
    private static readonly Guid ClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private ICoreAudioDeviceEnumerator? _native;

    private CoreAudioDeviceEnumerator(ICoreAudioDeviceEnumerator native)
    {
        _native = native;
    }

    public static bool TryCreate(out CoreAudioDeviceEnumerator? enumerator)
    {
        enumerator = null;
        try
        {
            if (!NativeWindowApi.TryCreateUiAutomation(ClassId, out ICoreAudioDeviceEnumerator? native)
                || native is null)
                return false;

            enumerator = new CoreAudioDeviceEnumerator(native);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryGetDefaultRenderDevice(out CoreAudioDevice? device)
    {
        device = null;
        if (_native is null)
            return false;

        try
        {
            int result = _native.GetDefaultAudioEndpoint(
                DataFlowRender,
                RoleMultimedia,
                out nint devicePointer);
            if (result < 0 || devicePointer == 0)
                return false;

            var native = CoreAudioNative.Wrap<ICoreAudioDevice>(devicePointer);
            if (native is null)
                return false;

            device = new CoreAudioDevice(native);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        CoreAudioNative.Release(_native);
        _native = null;
    }
}

internal sealed partial class CoreAudioDevice : IDisposable
{
    private const int ClsCtxAll = 23;
    private static readonly Guid EndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid SessionManagerId = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private ICoreAudioDevice? _native;
    private ICoreAudioEndpointVolume? _volume;
    private ICoreAudioSessionManager2? _sessionManager;

    public CoreAudioDevice(ICoreAudioDevice native)
    {
        _native = native;
        Id = ReadId(native);
        FriendlyName = string.IsNullOrWhiteSpace(Id) ? "Default audio device" : Id;
    }

    public string Id { get; }
    public string FriendlyName { get; }

    public bool TrySetMasterVolume(float volume)
    {
        if (!TryGetEndpointVolume(out var endpointVolume) || endpointVolume is null)
            return false;
        Guid context = Guid.Empty;
        return endpointVolume.SetMasterVolumeLevelScalar(volume, in context) >= 0;
    }

    public bool TryToggleMasterMute()
    {
        if (!TryGetEndpointVolume(out var endpointVolume) || endpointVolume is null)
            return false;

        if (endpointVolume.GetMute(out int muted) < 0)
            return false;
        Guid context = Guid.Empty;
        return endpointVolume.SetMute(muted == 0 ? 1 : 0, in context) >= 0;
    }

    public bool TryReadSnapshot(bool nativeOsdSuppressed, out VolumeSnapshot snapshot)
    {
        snapshot = VolumeSnapshot.Empty;
        if (!TryGetEndpointVolume(out var endpointVolume) || endpointVolume is null)
            return false;
        if (endpointVolume.GetMasterVolumeLevelScalar(out float masterVolume) < 0
            || endpointVolume.GetMute(out int muted) < 0)
            return false;

        var applications = new List<ApplicationVolumeSnapshot>();
        if (TryGetSessionEnumerator(out var sessionEnumerator) && sessionEnumerator is not null)
        {
            try
            {
                if (sessionEnumerator.GetCount(out int count) >= 0)
                {
                    for (int index = 0; index < count; index++)
                    {
                        if (sessionEnumerator.GetSession(index, out nint sessionPointer) < 0 || sessionPointer == 0)
                            continue;

                        using var session = CoreAudioSession.Create(sessionPointer, index);
                        if (session is not null && session.TryRead(out var application))
                            applications.Add(application);
                    }
                }
            }
            finally
            {
                CoreAudioNative.Release(sessionEnumerator);
            }
        }

        snapshot = new VolumeSnapshot(
            Id,
            FriendlyName,
            masterVolume,
            muted != 0,
            applications,
            nativeOsdSuppressed);
        return true;
    }

    public bool TrySetApplicationVolume(string sessionId, float volume)
        => WithSession(sessionId, session => session.TrySetVolume(volume));

    public bool TryToggleApplicationMute(string sessionId)
        => WithSession(sessionId, static session => session.TryToggleMute());

    public bool TryActivate<T>(Guid interfaceId, out T? activated)
        where T : class
    {
        activated = null;
        if (_native is null)
            return false;

        int result = _native.Activate(
            in interfaceId,
            ClsCtxAll,
            0,
            out nint interfacePointer);
        if (result < 0 || interfacePointer == 0)
            return false;

        activated = CoreAudioNative.Wrap<T>(interfacePointer);
        return activated is not null;
    }

    private bool WithSession(string sessionId, Func<CoreAudioSession, bool> action)
    {
        if (!TryGetSessionEnumerator(out var sessionEnumerator) || sessionEnumerator is null)
            return false;

        try
        {
            if (sessionEnumerator.GetCount(out int count) < 0)
                return false;

            for (int index = 0; index < count; index++)
            {
                if (sessionEnumerator.GetSession(index, out nint sessionPointer) < 0 || sessionPointer == 0)
                    continue;

                using var session = CoreAudioSession.Create(sessionPointer, index);
                if (session is not null
                    && string.Equals(session.SessionId, sessionId, StringComparison.Ordinal))
                    return action(session);
            }
            return false;
        }
        finally
        {
            CoreAudioNative.Release(sessionEnumerator);
        }
    }

    private bool TryGetEndpointVolume(out ICoreAudioEndpointVolume? endpointVolume)
    {
        endpointVolume = _volume;
        if (endpointVolume is not null)
            return true;
        if (_native is null)
            return false;

        int result = _native.Activate(
            in EndpointVolumeId,
            ClsCtxAll,
            0,
            out nint interfacePointer);
        if (result < 0 || interfacePointer == 0)
            return false;

        _volume = CoreAudioNative.Wrap<ICoreAudioEndpointVolume>(interfacePointer);
        endpointVolume = _volume;
        return endpointVolume is not null;
    }

    private bool TryGetSessionEnumerator(out ICoreAudioSessionEnumerator? sessionEnumerator)
    {
        sessionEnumerator = null;
        if (_sessionManager is null)
        {
            if (_native is null)
                return false;

            int result = _native.Activate(
                in SessionManagerId,
                ClsCtxAll,
                0,
                out nint interfacePointer);
            if (result < 0 || interfacePointer == 0)
                return false;
            _sessionManager = CoreAudioNative.Wrap<ICoreAudioSessionManager2>(interfacePointer);
        }

        if (_sessionManager is null
            || _sessionManager.GetSessionEnumerator(out nint sessionPointer) < 0
            || sessionPointer == 0)
            return false;

        sessionEnumerator = CoreAudioNative.Wrap<ICoreAudioSessionEnumerator>(sessionPointer);
        return sessionEnumerator is not null;
    }

    private static string ReadId(ICoreAudioDevice native)
    {
        try
        {
            if (native.GetId(out nint idPointer) >= 0)
                return CoreAudioNative.ReadCoTaskMemString(idPointer);
        }
        catch
        {
        }
        return "default";
    }

    public void Dispose()
    {
        CoreAudioNative.Release(_volume);
        _volume = null;
        CoreAudioNative.Release(_sessionManager);
        _sessionManager = null;
        CoreAudioNative.Release(_native);
        _native = null;
    }
}

internal sealed partial class CoreAudioSession : IDisposable
{
    private readonly ICoreAudioSessionControl? _control;
    private readonly ICoreAudioSessionControl2? _control2;
    private readonly ICoreAudioSimpleAudioVolume? _volume;
    private readonly uint _processId;
    private readonly int _index;

    private CoreAudioSession(
        ICoreAudioSessionControl control,
        ICoreAudioSessionControl2? control2,
        ICoreAudioSimpleAudioVolume? volume,
        int index,
        uint processId)
    {
        _control = control;
        _control2 = control2;
        _volume = volume;
        _index = index;
        _processId = processId;
    }

    public string SessionId => $"{_processId}:{_index}";

    public static CoreAudioSession? Create(nint pointer, int index)
    {
        var control2 = CoreAudioNative.QueryInterface<ICoreAudioSessionControl2>(pointer);
        var volume = CoreAudioNative.QueryInterface<ICoreAudioSimpleAudioVolume>(pointer);
        var control = CoreAudioNative.Wrap<ICoreAudioSessionControl>(pointer);
        if (control is null)
        {
            CoreAudioNative.Release(control2);
            CoreAudioNative.Release(volume);
            return null;
        }

        uint processId = 0;
        if (control2 is not null)
            _ = control2.GetProcessId(out processId);
        return new CoreAudioSession(control, control2, volume, index, processId);
    }

    public bool TryRead(out ApplicationVolumeSnapshot snapshot)
    {
        snapshot = default!;
        if (_volume is null)
            return false;
        if (_volume.GetMasterVolume(out float volume) < 0
            || _volume.GetMute(out int muted) < 0)
            return false;

        string displayName = ReadDisplayName();
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = ResolveProcessName(_processId);

        snapshot = new ApplicationVolumeSnapshot(
            SessionId,
            displayName,
            null,
            volume,
            muted != 0,
            _processId == Environment.ProcessId);
        return true;
    }

    public bool TrySetVolume(float volume)
    {
        if (_volume is null)
            return false;
        Guid context = Guid.Empty;
        return _volume.SetMasterVolume(volume, in context) >= 0;
    }

    public bool TryToggleMute()
    {
        if (_volume is null || _volume.GetMute(out int muted) < 0)
            return false;
        Guid context = Guid.Empty;
        return _volume.SetMute(muted == 0 ? 1 : 0, in context) >= 0;
    }

    private string ReadDisplayName()
    {
        if (_control is null)
            return string.Empty;
        try
        {
            if (_control.GetDisplayName(out nint displayNamePointer) >= 0)
                return CoreAudioNative.ReadCoTaskMemString(displayNamePointer);
        }
        catch
        {
        }
        return string.Empty;
    }

    private static string ResolveProcessName(uint processId)
    {
        if (processId == 0)
            return "System sounds";
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.IsNullOrWhiteSpace(process.ProcessName)
                ? $"Application {processId}"
                : process.ProcessName;
        }
        catch
        {
            return $"Application {processId}";
        }
    }

    public void Dispose()
    {
        CoreAudioNative.Release(_volume);
        CoreAudioNative.Release(_control2);
        CoreAudioNative.Release(_control);
    }
}

[GeneratedComInterface]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out nint endpoint);

    [PreserveSig]
    int GetDevice(nint id, out nint device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(nint client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(nint client);
}

[GeneratedComInterface]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioDevice
{
    [PreserveSig]
    int Activate(in Guid interfaceId, int classContext, nint activationParams, out nint interfacePointer);

    [PreserveSig]
    int OpenPropertyStore(int access, out nint properties);

    [PreserveSig]
    int GetId(out nint id);

    [PreserveSig]
    int GetState(out int state);
}

[GeneratedComInterface]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(nint notify);

    [PreserveSig]
    int UnregisterControlChangeNotify(nint notify);

    [PreserveSig]
    int GetChannelCount(out int channelCount);

    [PreserveSig]
    int SetMasterVolumeLevel(float levelDb, in Guid eventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float level, in Guid eventContext);

    [PreserveSig]
    int GetMasterVolumeLevel(out float levelDb);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig]
    int SetChannelVolumeLevel(uint channel, float levelDb, in Guid eventContext);

    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint channel, float level, in Guid eventContext);

    [PreserveSig]
    int GetChannelVolumeLevel(uint channel, out float levelDb);

    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint channel, out float level);

    [PreserveSig]
    int SetMute(int muted, in Guid eventContext);

    [PreserveSig]
    int GetMute(out int muted);

    [PreserveSig]
    int GetVolumeStepInfo(out uint step, out uint stepCount);

    [PreserveSig]
    int VolumeStepUp(in Guid eventContext);

    [PreserveSig]
    int VolumeStepDown(in Guid eventContext);

    [PreserveSig]
    int QueryHardwareSupport(out uint supportMask);

    [PreserveSig]
    int GetVolumeRange(out float minimumDb, out float maximumDb, out float incrementDb);
}

[GeneratedComInterface]
[Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioSessionManager2
{
    [PreserveSig]
    int GetAudioSessionControl(in Guid sessionId, uint streamFlags, out nint sessionControl);

    [PreserveSig]
    int GetSimpleAudioVolume(in Guid sessionId, uint streamFlags, out nint audioVolume);

    [PreserveSig]
    int GetSessionEnumerator(out nint sessionEnumerator);

    [PreserveSig]
    int RegisterSessionNotification(nint notification);

    [PreserveSig]
    int UnregisterSessionNotification(nint notification);

    [PreserveSig]
    int RegisterDuckNotification(nint sessionId, nint notification);

    [PreserveSig]
    int UnregisterDuckNotification(nint notification);
}

[GeneratedComInterface]
[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioSessionEnumerator
{
    [PreserveSig]
    int GetCount(out int count);

    [PreserveSig]
    int GetSession(int index, out nint session);
}

[GeneratedComInterface]
[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioSessionControl
{
    [PreserveSig]
    int GetState(out int state);

    [PreserveSig]
    int GetDisplayName(out nint displayName);

    [PreserveSig]
    int SetDisplayName(nint displayName, in Guid eventContext);

    [PreserveSig]
    int GetIconPath(out nint iconPath);

    [PreserveSig]
    int SetIconPath(nint iconPath, in Guid eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingId);

    [PreserveSig]
    int SetGroupingParam(in Guid groupingId, in Guid eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(nint client);

    [PreserveSig]
    int UnregisterAudioSessionNotification(nint client);
}

[GeneratedComInterface]
[Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioSessionControl2
{
    [PreserveSig]
    int GetState(out int state);

    [PreserveSig]
    int GetDisplayName(out nint displayName);

    [PreserveSig]
    int SetDisplayName(nint displayName, in Guid eventContext);

    [PreserveSig]
    int GetIconPath(out nint iconPath);

    [PreserveSig]
    int SetIconPath(nint iconPath, in Guid eventContext);

    [PreserveSig]
    int GetGroupingParam(out Guid groupingId);

    [PreserveSig]
    int SetGroupingParam(in Guid groupingId, in Guid eventContext);

    [PreserveSig]
    int RegisterAudioSessionNotification(nint client);

    [PreserveSig]
    int UnregisterAudioSessionNotification(nint client);

    [PreserveSig]
    int GetSessionIdentifier(out nint identifier);

    [PreserveSig]
    int GetSessionInstanceIdentifier(out nint identifier);

    [PreserveSig]
    int GetProcessId(out uint processId);

    [PreserveSig]
    int IsSystemSoundsSession();

    [PreserveSig]
    int SetDuckingPreference(int optOut);
}

[GeneratedComInterface]
[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioSimpleAudioVolume
{
    [PreserveSig]
    int SetMasterVolume(float level, in Guid eventContext);

    [PreserveSig]
    int GetMasterVolume(out float level);

    [PreserveSig]
    int SetMute(int muted, in Guid eventContext);

    [PreserveSig]
    int GetMute(out int muted);
}

[GeneratedComInterface]
[Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioClient
{
    [PreserveSig]
    int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, nint format, in Guid sessionId);

    [PreserveSig]
    int GetBufferSize(out uint bufferSize);

    [PreserveSig]
    int GetStreamLatency(out long latency);

    [PreserveSig]
    int GetCurrentPadding(out uint padding);

    [PreserveSig]
    int IsFormatSupported(int shareMode, nint format, out nint closestMatch);

    [PreserveSig]
    int GetMixFormat(out nint format);

    [PreserveSig]
    int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);

    [PreserveSig]
    int Start();

    [PreserveSig]
    int Stop();

    [PreserveSig]
    int Reset();

    [PreserveSig]
    int SetEventHandle(nint eventHandle);

    [PreserveSig]
    int GetService(in Guid interfaceId, out nint service);
}

[GeneratedComInterface]
[Guid("C8ADBD64-E71E-48A0-A4DE-185C3950CDEB")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICoreAudioCaptureClient
{
    [PreserveSig]
    int GetBuffer(out nint data, out uint frames, out uint flags, out ulong devicePosition, out ulong qpcPosition);

    [PreserveSig]
    int ReleaseBuffer(uint frames);

    [PreserveSig]
    int GetNextPacketSize(out uint frames);
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct CoreAudioWaveFormat
{
    public ushort FormatTag;
    public ushort Channels;
    public uint SamplesPerSecond;
    public uint AverageBytesPerSecond;
    public ushort BlockAlign;
    public ushort BitsPerSample;
    public ushort ExtraSize;
}
