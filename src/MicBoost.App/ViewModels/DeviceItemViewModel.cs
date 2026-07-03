using CommunityToolkit.Mvvm.ComponentModel;
using MicBoost.Audio.Devices;

namespace MicBoost.App.ViewModels;

/// <summary>Display wrapper around an <see cref="AudioDeviceInfo"/> for the device list.</summary>
public sealed partial class DeviceItemViewModel : ObservableObject
{
    public DeviceItemViewModel(AudioDeviceInfo info)
    {
        Id = info.Id;
        Name = info.Name;
        IsDefault = info.IsDefault;
    }

    public string Id { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool isDefault;
}
