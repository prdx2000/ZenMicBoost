namespace MicBoost.Audio.Devices;

/// <summary>Immutable snapshot of an audio capture device, keyed by its stable WASAPI device ID.</summary>
public sealed record AudioDeviceInfo(string Id, string Name, bool IsDefault, bool IsAvailable);
