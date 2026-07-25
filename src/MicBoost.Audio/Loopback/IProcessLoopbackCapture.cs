using NAudio.Wave;

namespace MicBoost.Audio.Loopback;

/// <summary>
/// Captures only the audio rendered by one process (and its child processes) via the
/// Windows 10 2004+ WASAPI process-loopback virtual device, independent of whatever
/// physical output device that process happens to be playing through.
/// </summary>
public interface IProcessLoopbackCapture : IDisposable
{
    /// <summary>Raised with newly captured audio while running.</summary>
    event EventHandler<WaveInEventArgs>? DataAvailable;

    /// <summary>Raised if the capture stops unexpectedly (e.g. the target process exited).</summary>
    event EventHandler<Exception>? CaptureError;

    /// <summary>Starts capturing <paramref name="processId"/>'s render audio in <paramref name="format"/>.</summary>
    Task StartAsync(int processId, WaveFormat format);

    /// <summary>Stops capturing, if started. Safe to call when not started.</summary>
    void Stop();
}
