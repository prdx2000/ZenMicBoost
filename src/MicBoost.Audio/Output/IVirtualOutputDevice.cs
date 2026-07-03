using NAudio.Wave;

namespace MicBoost.Audio.Output;

/// <summary>
/// Abstracts the "render into a virtual cable" stage so a different virtual audio
/// driver could be swapped in later without touching the capture/DSP/engine code.
/// </summary>
public interface IVirtualOutputDevice : IDisposable
{
    /// <summary>True if a compatible virtual cable driver is installed and usable.</summary>
    bool IsAvailable { get; }

    /// <summary>Friendly name of the virtual cable's playback endpoint, if available.</summary>
    string? DeviceName { get; }

    /// <summary>Starts rendering <paramref name="source"/> into the virtual cable's playback endpoint.</summary>
    void Start(ISampleProvider source);

    /// <summary>Stops rendering, if started.</summary>
    void Stop();
}
