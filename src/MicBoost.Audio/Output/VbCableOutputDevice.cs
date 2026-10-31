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
        _output = CreateOutput(device, source);
        _output.Play();
    }

    /// <summary>
    /// Opens the cable in exclusive mode where possible. A shared-mode stream goes through the
    /// Windows audio engine as MicBoost's own app audio, and anything doing per-process loopback
    /// capture picks it up there: Discord streaming a whole screen records every app except
    /// itself, so the boosted mic ended up in the stream on top of the voice channel. Exclusive
    /// streams bypass the engine, so no loopback capture can see them.
    /// </summary>
    private static WasapiOut CreateOutput(MMDevice device, ISampleProvider source)
    {
        var exclusive = new WasapiOut(device, AudioClientShareMode.Exclusive, true, LatencyMs);

        try
        {
            exclusive.Init(source);
            return exclusive;
        }
        catch (Exception)
        {
            // Exclusive control disabled on the cable, another app already has it open, or no
            // usable format. Shared mode still works; it's just visible to loopback capture again.
            exclusive.Dispose();
        }

        var shared = new WasapiOut(device, AudioClientShareMode.Shared, true, LatencyMs);
        shared.Init(source);
        return shared;
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
