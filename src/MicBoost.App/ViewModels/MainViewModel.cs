using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MicBoost.App.Services;
using MicBoost.Audio.Devices;
using MicBoost.Audio.Dsp;
using MicBoost.Audio.Engine;
using MicBoost.Audio.Output;
using MicBoost.Audio.Settings;
using Wpf.Ui.Appearance;

namespace MicBoost.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAudioDeviceService _deviceService;
    private readonly IMicBoostEngine _engine;
    private readonly ISettingsService _settingsService;
    private readonly IVirtualCableDetector _cableDetector;
    private readonly IStartupService _startupService;

    private AppSettings _settings = new();
    private DispatcherTimer? _meterTimer;
    private DispatcherTimer? _saveTimer;
    private bool _isLoadingDevice;
    private bool _initialized;

    public MainViewModel(
        IAudioDeviceService deviceService,
        IMicBoostEngine engine,
        ISettingsService settingsService,
        IVirtualCableDetector cableDetector,
        IStartupService startupService)
    {
        _deviceService = deviceService;
        _engine = engine;
        _settingsService = settingsService;
        _cableDetector = cableDetector;
        _startupService = startupService;
    }

    public double MinGainDb => GainMath.MinDb;

    public double MaxGainDb => GainMath.MaxDb;

    public double GainStepDb => GainMath.StepDb;

    public double MinBassDb => BassMath.MinDb;

    public double MaxBassDb => BassMath.MaxDb;

    public double BassStepDb => BassMath.StepDb;

    public bool IsExiting { get; private set; }

    public string CableDownloadUrl => VirtualCableLocator.DownloadUrl;

    [ObservableProperty]
    private ObservableCollection<DeviceItemViewModel> devices = new();

    [ObservableProperty]
    private DeviceItemViewModel? selectedDevice;

    [ObservableProperty]
    private double gainDb = GainMath.DefaultDb;

    [ObservableProperty]
    private double bassDb = BassMath.DefaultDb;

    [ObservableProperty]
    private bool isLimiting;

    [ObservableProperty]
    private float inputLevel;

    [ObservableProperty]
    private float outputLevel;

    [ObservableProperty]
    private bool isMuted;

    [ObservableProperty]
    private bool isCableInstalled;

    [ObservableProperty]
    private string cableStatusMessage = "Checking for a virtual audio cable...";

    [ObservableProperty]
    private bool launchOnStartup;

    [ObservableProperty]
    private bool minimizeToTray = true;

    [ObservableProperty]
    private bool isDarkTheme = true;

    [ObservableProperty]
    private bool isEngineRunning;

    [ObservableProperty]
    private string? statusMessage;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _settings = _settingsService.Load();

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer!.Stop();
            _settingsService.Save(_settings);
        };

        MinimizeToTray = _settings.MinimizeToTray;
        LaunchOnStartup = _startupService.IsEnabled;
        IsDarkTheme = _settings.Theme == AppTheme.Dark;

        _deviceService.DevicesChanged += (_, _) => System.Windows.Application.Current.Dispatcher.Invoke(RefreshDevices);

        RefreshCableStatus();
        RefreshDevices();

        _meterTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _meterTimer.Tick += (_, _) => PollMeters();
        _meterTimer.Start();
    }

    private void RefreshDevices()
    {
        var infos = _deviceService.GetCaptureDevices();
        var currentIds = infos.Select(i => i.Id).ToHashSet();

        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(Devices[i].Id))
            {
                Devices.RemoveAt(i);
            }
        }

        var existingIds = Devices.Select(d => d.Id).ToHashSet();
        foreach (var info in infos)
        {
            var existing = Devices.FirstOrDefault(d => d.Id == info.Id);
            if (existing is null)
            {
                Devices.Add(new DeviceItemViewModel(info));
            }
            else
            {
                existing.IsDefault = info.IsDefault;
            }
        }

        if (SelectedDevice is not null && !currentIds.Contains(SelectedDevice.Id))
        {
            StatusMessage = "Your selected microphone was disconnected. Please choose another.";
            SelectedDevice = Devices.FirstOrDefault();
        }
        else if (SelectedDevice is null)
        {
            var wantedId = _settings.LastSelectedDeviceId;
            SelectedDevice = Devices.FirstOrDefault(d => d.Id == wantedId) ?? Devices.FirstOrDefault();
            if (SelectedDevice is null)
            {
                StatusMessage = Devices.Count == 0 ? "No microphones detected." : null;
            }
        }
    }

    private void RefreshCableStatus()
    {
        var status = _cableDetector.Detect();
        IsCableInstalled = status.IsInstalled;
        CableStatusMessage = status.IsInstalled
            ? $"Rendering into {status.RenderDeviceName}"
            : "No virtual audio cable detected. Install VB-CABLE so other apps can use your boosted mic.";

        if (status.IsInstalled && SelectedDevice is not null && !IsEngineRunning)
        {
            StartEngineForSelectedDevice();
        }
    }

    private void StartEngineForSelectedDevice()
    {
        if (SelectedDevice is null || !IsCableInstalled)
        {
            _engine.Stop();
            IsEngineRunning = false;
            return;
        }

        try
        {
            _engine.Start(SelectedDevice.Id, GainDb);
            _engine.BassDb = BassDb;
            _engine.IsMuted = IsMuted;
            IsEngineRunning = true;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            IsEngineRunning = false;
            StatusMessage = $"Couldn't start MicBoost for this microphone: {ex.Message}";
        }
    }

    private void PollMeters()
    {
        InputLevel = _engine.InputLevel;
        OutputLevel = _engine.OutputLevel;
        IsLimiting = _engine.IsLimiting;
    }

    partial void OnSelectedDeviceChanged(DeviceItemViewModel? value)
    {
        if (value is null)
        {
            _engine.Stop();
            IsEngineRunning = false;
            return;
        }

        _isLoadingDevice = true;
        GainDb = _settings.GetGainOrDefault(value.Id);
        BassDb = _settings.GetBassOrDefault(value.Id);
        _isLoadingDevice = false;

        _settings.LastSelectedDeviceId = value.Id;
        StartEngineForSelectedDevice();
        DebounceSaveSettings();
    }

    partial void OnGainDbChanged(double value)
    {
        var clamped = GainMath.ClampDb(value);
        if (clamped != value)
        {
            GainDb = clamped;
            return;
        }

        _engine.GainDb = clamped;

        if (SelectedDevice is not null && !_isLoadingDevice)
        {
            _settings.SetGain(SelectedDevice.Id, clamped);
            DebounceSaveSettings();
        }
    }

    partial void OnBassDbChanged(double value)
    {
        var clamped = BassMath.ClampDb(value);
        if (clamped != value)
        {
            BassDb = clamped;
            return;
        }

        _engine.BassDb = clamped;

        if (SelectedDevice is not null && !_isLoadingDevice)
        {
            _settings.SetBass(SelectedDevice.Id, clamped);
            DebounceSaveSettings();
        }
    }

    partial void OnIsMutedChanged(bool value) => _engine.IsMuted = value;

    partial void OnLaunchOnStartupChanged(bool value)
    {
        _startupService.SetEnabled(value);
        _settings.LaunchOnStartup = value;
        DebounceSaveSettings();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settings.MinimizeToTray = value;
        DebounceSaveSettings();
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplicationThemeManager.Apply(
            value ? ApplicationTheme.Dark : ApplicationTheme.Light,
            Wpf.Ui.Controls.WindowBackdropType.Mica);

        _settings.Theme = value ? AppTheme.Dark : AppTheme.Light;
        DebounceSaveSettings();
    }

    private void DebounceSaveSettings()
    {
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    public void SaveSettingsNow() => _settingsService.Save(_settings);

    [RelayCommand]
    private void IncreaseGain() => GainDb = GainMath.ClampDb(GainDb + GainMath.StepDb);

    [RelayCommand]
    private void DecreaseGain() => GainDb = GainMath.ClampDb(GainDb - GainMath.StepDb);

    [RelayCommand]
    private void ResetGain() => GainDb = GainMath.DefaultDb;

    [RelayCommand]
    private void IncreaseBass() => BassDb = BassMath.ClampDb(BassDb + BassMath.StepDb);

    [RelayCommand]
    private void DecreaseBass() => BassDb = BassMath.ClampDb(BassDb - BassMath.StepDb);

    [RelayCommand]
    private void ResetBass() => BassDb = BassMath.DefaultDb;

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void RefreshDevicesList() => RefreshDevices();

    [RelayCommand]
    private void RecheckCable() => RefreshCableStatus();

    [RelayCommand]
    private void OpenDownloadPage() => Process.Start(new ProcessStartInfo(CableDownloadUrl) { UseShellExecute = true });

    [RelayCommand]
    private void Exit()
    {
        IsExiting = true;
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _meterTimer?.Stop();
        _saveTimer?.Stop();
    }
}
