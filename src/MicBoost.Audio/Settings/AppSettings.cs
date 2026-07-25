namespace MicBoost.Audio.Settings;

public enum AppTheme
{
    Dark,
    Light
}

/// <summary>Persisted MicBoost configuration. Serialized as JSON to %AppData%/MicBoost/settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Per-device gain in dB, keyed by stable WASAPI device ID (not friendly name).</summary>
    public Dictionary<string, double> DeviceGainsDb { get; set; } = new();

    /// <summary>Per-device low-shelf bass adjustment in dB, keyed by stable WASAPI device ID.</summary>
    public Dictionary<string, double> DeviceBassDb { get; set; } = new();

    public string? LastSelectedDeviceId { get; set; }

    public bool LaunchOnStartup { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    /// <summary>
    /// Master on/off for app-audio mirroring. Kept separate from
    /// <see cref="AppAudioProcessName"/> so switching it off stops mirroring without
    /// forgetting which app was chosen. Flipping it back on resumes that app.
    /// </summary>
    public bool AppAudioEnabled { get; set; } = true;

    /// <summary>
    /// Executable name (e.g. "spotify.exe") of the process to mirror into the output, mixed
    /// with the mic. Kept by name rather than PID since the PID changes every launch; MicBoost
    /// reconnects automatically once a process with this name shows up with an audio session.
    /// Null/empty means no app is chosen; see <see cref="AppAudioEnabled"/> for the master switch.
    /// </summary>
    public string? AppAudioProcessName { get; set; }

    /// <summary>Linear volume (0..1) applied to the mirrored app audio.</summary>
    public double AppAudioVolume { get; set; } = 1.0;

    public double GetGainOrDefault(string deviceId)
        => DeviceGainsDb.TryGetValue(deviceId, out var db) ? db : Dsp.GainMath.DefaultDb;

    public void SetGain(string deviceId, double gainDb)
        => DeviceGainsDb[deviceId] = Dsp.GainMath.ClampDb(gainDb);

    public double GetBassOrDefault(string deviceId)
        => DeviceBassDb.TryGetValue(deviceId, out var db) ? db : Dsp.BassMath.DefaultDb;

    public void SetBass(string deviceId, double bassDb)
        => DeviceBassDb[deviceId] = Dsp.BassMath.ClampDb(bassDb);
}
