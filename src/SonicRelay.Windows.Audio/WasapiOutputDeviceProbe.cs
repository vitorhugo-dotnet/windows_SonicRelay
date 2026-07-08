using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SonicRelay.Windows.Audio;

/// <summary>
/// Enumerates the active Windows render endpoints via Core Audio (MMDevice) so
/// the user can pick which output to publish. Purely read-only — it installs no
/// driver, changes no device setting, and needs no elevation. Fully defensive:
/// any COM failure yields an empty list rather than throwing.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WasapiOutputDeviceProbe : IAudioOutputDeviceProbe
{
    private const uint DeviceStateActive = 0x00000001;

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        var results = new List<AudioOutputDevice>();
        IMMDeviceEnumerator? enumerator = null;
        var comInitialized = NativeMethods.CoInitializeEx(IntPtr.Zero, 0) >= 0;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            var defaultId = TryGetDefaultId(enumerator);

            if (enumerator.EnumAudioEndpoints(EDataFlow.Render, DeviceStateActive, out var collection) < 0
                || collection is null)
            {
                return results;
            }
            try
            {
                if (collection.GetCount(out var count) < 0) return results;
                for (uint i = 0; i < count; i++)
                {
                    if (collection.Item(i, out var device) < 0 || device is null) continue;
                    try
                    {
                        var id = WasapiLoopbackBackend.GetDeviceId(device);
                        var name = WasapiLoopbackBackend.TryGetDeviceName(device) ?? "Unknown output device";
                        results.Add(new AudioOutputDevice(id, name, string.Equals(id, defaultId, StringComparison.Ordinal)));
                    }
                    catch (WasapiException)
                    {
                        // Skip a device whose id/name cannot be read.
                    }
                    finally { ReleaseCom(device); }
                }
            }
            finally { ReleaseCom(collection); }
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or WasapiException)
        {
            // Enumeration is best-effort; the caller falls back to the default device.
        }
        finally
        {
            ReleaseCom(enumerator);
            if (comInitialized) NativeMethods.CoUninitialize();
        }
        return results;
    }

    private static string? TryGetDefaultId(IMMDeviceEnumerator enumerator)
    {
        if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var endpoint) < 0
            || endpoint is null)
        {
            return null;
        }
        try { return WasapiLoopbackBackend.GetDeviceId(endpoint); }
        catch (WasapiException) { return null; }
        finally { ReleaseCom(endpoint); }
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
