namespace MicBoost.Audio.Loopback;

/// <summary>Lists processes that can currently be mirrored via <see cref="IProcessLoopbackCapture"/>.</summary>
public interface IAppAudioSessionService
{
    /// <summary>
    /// One entry per distinct process with an audio session on the default playback device
    /// (whether or not it's actively making sound right now).
    /// </summary>
    IReadOnlyList<ProcessAudioSessionInfo> GetActiveSessions();
}
