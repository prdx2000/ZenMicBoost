namespace MicBoost.Audio.Engine;

/// <summary>
/// Orchestrates the capture -> gain -> virtual-cable pipeline for the currently selected mic.
/// </summary>
public interface IMicBoostEngine : IDisposable
{
    bool IsRunning { get; }

    /// <summary>Current gain in dB. Settable while running for real-time adjustment.</summary>
    double GainDb { get; set; }

    /// <summary>Low-shelf bass adjustment in dB, independent of overall gain. Settable while running.</summary>
    double BassDb { get; set; }

    /// <summary>True if the limiter is actively clamping the signal.</summary>
    bool IsLimiting { get; }

    /// <summary>Silences the virtual mic output without discarding the configured gain.</summary>
    bool IsMuted { get; set; }

    /// <summary>Peak input level (0..1+) before gain is applied.</summary>
    float InputLevel { get; }

    /// <summary>Peak output level (0..1+) after gain and limiting.</summary>
    float OutputLevel { get; }

    /// <summary>Raised when the capture or render pipeline fails unexpectedly while running.</summary>
    event EventHandler<Exception>? EngineError;

    /// <summary>Id of the process currently selected for app-audio mirroring, or null if none.</summary>
    int? AppAudioProcessId { get; }

    /// <summary>True once the app-audio branch is actually capturing (vs. still connecting, or failed).</summary>
    bool IsAppAudioActive { get; }

    /// <summary>Linear volume (0..1) applied to mirrored app audio before mixing into the output. Settable while running.</summary>
    double AppAudioVolume { get; set; }

    /// <summary>Raised when app-audio capture fails to start, or stops unexpectedly while running.</summary>
    event EventHandler<Exception>? AppAudioError;

    void Start(string captureDeviceId, double initialGainDb);

    void Stop();

    /// <summary>
    /// Selects which process's audio is mirrored into the output, mixed with the mic. Pass
    /// null to stop mirroring. Persists across mic switches (<see cref="Start"/>/<see cref="Stop"/>)
    /// and reconnects automatically once the engine is running again.
    /// </summary>
    Task SetAppAudioProcessAsync(int? processId);
}
