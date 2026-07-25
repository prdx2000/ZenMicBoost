namespace MicBoost.Audio.Loopback;

/// <summary>One process currently known to the default playback device's audio session manager.</summary>
public sealed record ProcessAudioSessionInfo(int ProcessId, string ProcessName, string DisplayName);
