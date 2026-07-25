using NAudio.CoreAudioApi;

namespace MicBoost.Audio.Output;

/// <summary>
/// Locates the installed virtual audio cable's endpoints by friendly name.
/// VB-CABLE (the default supported driver) installs a playback endpoint named
/// "CABLE Input (VB-Audio Virtual Cable)", which MicBoost renders into, and a matching
/// recording endpoint "CABLE Output (VB-Audio Virtual Cable)" that other apps
/// (Discord, Zoom, etc.) select as their microphone.
/// </summary>
public static class VirtualCableLocator
{
    /// <summary>Where to download VB-CABLE if it isn't detected.</summary>
    public const string DownloadUrl = "https://vb-audio.com/Cable/";

    private static readonly string[] NameHints = ["CABLE Input", "VB-Audio Virtual Cable"];

    /// <summary>True if this friendly name belongs to the virtual cable itself, not a real physical/app mic.</summary>
    public static bool IsVirtualCableDevice(string friendlyName)
        => NameHints.Any(hint => friendlyName.Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds the virtual cable's playback (render) endpoint, i.e. what MicBoost writes audio to.</summary>
    public static MMDevice? FindRenderEndpoint(MMDeviceEnumerator enumerator)
        => FindEndpoint(enumerator, DataFlow.Render);

    /// <summary>Finds the virtual cable's recording endpoint, i.e. what downstream apps pick as their mic.</summary>
    public static MMDevice? FindCaptureEndpoint(MMDeviceEnumerator enumerator)
        => FindEndpoint(enumerator, DataFlow.Capture);

    private static MMDevice? FindEndpoint(MMDeviceEnumerator enumerator, DataFlow flow)
    {
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            if (IsVirtualCableDevice(device.FriendlyName))
            {
                return device;
            }

            device.Dispose();
        }

        return null;
    }
}
