namespace MicBoost.App.Services;

/// <summary>Owns the system tray icon: quick mute/gain status and show/exit actions.</summary>
public interface ITrayIconService : IDisposable
{
    void Initialize();

    void SetTooltip(string text);
}
