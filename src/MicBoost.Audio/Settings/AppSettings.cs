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

    public double GetGainOrDefault(string deviceId)
        => DeviceGainsDb.TryGetValue(deviceId, out var db) ? db : Dsp.GainMath.DefaultDb;

    public void SetGain(string deviceId, double gainDb)
        => DeviceGainsDb[deviceId] = Dsp.GainMath.ClampDb(gainDb);

    public double GetBassOrDefault(string deviceId)
        => DeviceBassDb.TryGetValue(deviceId, out var db) ? db : Dsp.BassMath.DefaultDb;

    public void SetBass(string deviceId, double bassDb)
        => DeviceBassDb[deviceId] = Dsp.BassMath.ClampDb(bassDb);
}
