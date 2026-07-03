namespace MicBoost.App.Services;

/// <summary>Controls whether MicBoost launches automatically when Windows starts.</summary>
public interface IStartupService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
