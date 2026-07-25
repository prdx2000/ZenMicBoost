using Windows.Media.Control;

namespace MicBoost.Audio.Loopback;

/// <summary>
/// Wraps <see cref="GlobalSystemMediaTransportControlsSessionManager"/> (the same "now playing"
/// registry Windows' own volume flyout and media overlay read from) so the app-audio picker can
/// show what a mirrored app is actually playing, not just its name.
/// </summary>
public sealed class MediaSessionInfoService : IMediaSessionInfoService
{
    public async Task<IReadOnlyList<MediaSession>> GetNowPlayingSessionsAsync()
    {
        var result = new List<MediaSession>();

        GlobalSystemMediaTransportControlsSessionManager manager;
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception)
        {
            // No SMTC registry available (very old Windows, or a locked-down system) — no now-playing info.
            return result;
        }

        foreach (var session in manager.GetSessions())
        {
            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                if (props is null)
                {
                    continue;
                }

                result.Add(new MediaSession(session.SourceAppUserModelId, new MediaInfo(props.Title, props.Artist)));
            }
            catch (Exception)
            {
                // That session stopped reporting between enumeration and lookup — just skip it.
            }
        }

        return result;
    }
}
