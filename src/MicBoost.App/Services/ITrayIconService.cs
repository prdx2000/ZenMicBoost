namespace MicBoost.App.Services;

/// <summary>Owns the system tray icon: quick mute/gain status and show/exit actions.</summary>
/// <remarks>Also the single place that knows how to bring the main window back from the tray.</remarks>
public interface ITrayIconService : IDisposable
{
    void Initialize();

    void SetTooltip(string text);

    /// <summary>Restores and focuses the main window, creating it if it was never shown.</summary>
    void ShowMainWindow();
}
