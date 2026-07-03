using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MicBoost.Audio.Output;

/// <summary>
/// Renders the processed mic stream into VB-CABLE's playback endpoint ("CABLE Input"),
/// whose matching recording endpoint ("CABLE Output") other apps select as their mic.
/// </summary>
public sealed class VbCableOutputDevice : IVirtualOutputDevice
{
    private const int LatencyMs = 20;

    private WasapiOut? _output;
    private MMDevice? _renderDevice;

    public bool IsAvailable
    {
        get
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = VirtualCableLocator.FindRenderEndpoint(enumerator);
            return device is not null;
        }
    }

    public string? DeviceName => _renderDevice?.FriendlyName;

    public void Start(ISampleProvider source)
    {
        Stop();

        var enumerator = new MMDeviceEnumerator();
        var device = VirtualCableLocator.FindRenderEndpoint(enumerator)
            ?? throw new InvalidOperationException(
                "No virtual audio cable playback endpoint found. Install VB-CABLE and try again.");
        enumerator.Dispose();

        _renderDevice = device;
        _output = new WasapiOut(device, AudioClientShareMode.Shared, true, LatencyMs);
        _output.Init(source);
        _output.Play();
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;

        _renderDevice?.Dispose();
        _renderDevice = null;
    }

    public void Dispose() => Stop();
}
