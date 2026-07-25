using MicBoost.Audio.Output;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MicBoost.Audio.Devices;

/// <summary>
/// Wraps <see cref="MMDeviceEnumerator"/> to list capture devices and to raise
/// <see cref="DevicesChanged"/> live when the user plugs/unplugs a microphone.
/// </summary>
public sealed class AudioDeviceService : IAudioDeviceService, IMMNotificationClient
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public event EventHandler? DevicesChanged;

    public AudioDeviceService()
    {
        _enumerator.RegisterEndpointNotificationCallback(this);
    }

    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        var defaultId = TryGetDefaultCaptureId();

        var result = new List<AudioDeviceInfo>();

        // Exclude the virtual cable's own recording endpoint. It's the output of this app's
        // pipeline, not a physical mic that makes sense to select as a source.
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            if (!VirtualCableLocator.IsVirtualCableDevice(device.FriendlyName))
            {
                result.Add(ToInfo(device, defaultId));
            }

            device.Dispose();
        }

        return result;
    }

    public AudioDeviceInfo? GetDevice(string deviceId)
    {
        try
        {
            using var device = _enumerator.GetDevice(deviceId);
            return ToInfo(device, TryGetDefaultCaptureId());
        }
        catch (Exception)
        {
            // Device no longer exists (unplugged, disabled, etc).
            return null;
        }
    }

    private string? TryGetDefaultCaptureId()
    {
        try
        {
            using var def = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return def.ID;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static AudioDeviceInfo ToInfo(MMDevice device, string? defaultId) => new(
        Id: device.ID,
        Name: device.FriendlyName,
        IsDefault: device.ID == defaultId,
        IsAvailable: device.State == DeviceState.Active);

    public void Dispose()
    {
        _enumerator.UnregisterEndpointNotificationCallback(this);
        _enumerator.Dispose();
    }

    // IMMNotificationClient: any change to a capture endpoint re-raises DevicesChanged.
    // Consumers re-enumerate via GetCaptureDevices() rather than trying to diff here.

    void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState newState)
        => DevicesChanged?.Invoke(this, EventArgs.Empty);

    void IMMNotificationClient.OnDeviceAdded(string pwstrDeviceId)
        => DevicesChanged?.Invoke(this, EventArgs.Empty);

    void IMMNotificationClient.OnDeviceRemoved(string deviceId)
        => DevicesChanged?.Invoke(this, EventArgs.Empty);

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow == DataFlow.Capture)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    void IMMNotificationClient.OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        // Not relevant to the device list; ignored.
    }
}
