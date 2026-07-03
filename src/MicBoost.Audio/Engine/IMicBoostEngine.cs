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

    void Start(string captureDeviceId, double initialGainDb);

    void Stop();
}
