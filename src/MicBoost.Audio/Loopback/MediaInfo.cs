namespace MicBoost.Audio.Loopback;

/// <summary>What a process reports as "now playing" via the System Media Transport Controls.</summary>
public sealed record MediaInfo(string? Title, string? Artist)
{
    /// <summary>"Title — Artist", just "Title" if there's no artist, or null if there's nothing worth showing.</summary>
    public string? DisplayText()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(Artist) ? Title : $"{Title} — {Artist}";
    }
}

/// <summary>One app's now-playing media, matched back to the process that owns it.</summary>
public sealed record MediaSession(string SourceAppUserModelId, MediaInfo Info);
