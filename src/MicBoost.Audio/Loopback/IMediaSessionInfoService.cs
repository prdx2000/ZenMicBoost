namespace MicBoost.Audio.Loopback;

/// <summary>Reads "now playing" media info from the System Media Transport Controls (SMTC).</summary>
public interface IMediaSessionInfoService
{
    /// <summary>
    /// One entry per app currently reporting media via SMTC. <see cref="MediaSession.SourceAppUserModelId"/>
    /// is a best-effort app identifier (often just the process name, e.g. "Chrome" or "Spotify").
    /// Match it against <see cref="ProcessAudioSessionInfo.ProcessName"/> to find the owning process.
    /// </summary>
    Task<IReadOnlyList<MediaSession>> GetNowPlayingSessionsAsync();
}
