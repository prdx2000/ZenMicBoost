namespace MicBoost.Audio.Devices;

/// <summary>Enumerates audio capture (microphone) devices and reports hot-plug changes live.</summary>
public interface IAudioDeviceService : IDisposable
{
    /// <summary>Raised whenever a capture device is added, removed, or its state changes.</summary>
    event EventHandler? DevicesChanged;

    IReadOnlyList<AudioDeviceInfo> GetCaptureDevices();

    AudioDeviceInfo? GetDevice(string deviceId);
}
