using CommunityToolkit.Mvvm.ComponentModel;
using MicBoost.Audio.Loopback;

namespace MicBoost.App.ViewModels;

/// <summary>Display wrapper around a <see cref="ProcessAudioSessionInfo"/> for the app-audio list.</summary>
public sealed partial class AppAudioSessionItemViewModel : ObservableObject
{
    public AppAudioSessionItemViewModel(ProcessAudioSessionInfo info)
    {
        ProcessId = info.ProcessId;
        ProcessName = info.ProcessName;
        DisplayName = info.DisplayName;
    }

    public int ProcessId { get; }

    public string ProcessName { get; }

    public string DisplayName { get; }

    /// <summary>"Title - Artist" from SMTC if this process is currently reporting one, else null.</summary>
    [ObservableProperty]
    private string? nowPlaying;
}
