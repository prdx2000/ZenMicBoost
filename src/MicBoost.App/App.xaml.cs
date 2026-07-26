using System.Windows;
using MicBoost.App.Services;
using MicBoost.App.ViewModels;
using MicBoost.App.Views;
using MicBoost.Audio.Devices;
using MicBoost.Audio.Engine;
using MicBoost.Audio.Loopback;
using MicBoost.Audio.Output;
using MicBoost.Audio.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MicBoost.App;

public partial class App : System.Windows.Application
{
    // Session-scoped, so a second sign-in gets its own instance.
    private const string InstanceMutexName = @"Local\MicBoost.SingleInstance";
    private const string ShowWindowSignalName = @"Local\MicBoost.ShowWindow";

    private IHost? _host;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showWindowSignal;
    private CancellationTokenSource? _signalListenerCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Launching MicBoost while it's already running (typically from the Start Menu, while an
        // autostarted copy sits in the tray) used to start a second instance. Both then fought
        // over the mic and the virtual cable, and neither tray icon opened the window you wanted.
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            if (EventWaitHandle.TryOpenExisting(ShowWindowSignalName, out var running))
            {
                running.Set();
                running.Dispose();
            }

            Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        _host.Start();

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;

        var viewModel = _host.Services.GetRequiredService<MainViewModel>();
        viewModel.Initialize();

        var trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
        trayIconService.Initialize();

        var startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
        {
            window.Show();
        }

        ListenForShowWindowRequests(trayIconService);
    }

    /// <summary>
    /// Watches for a later launch signalling that the user wants the window, and surfaces it.
    /// </summary>
    private void ListenForShowWindowRequests(ITrayIconService trayIconService)
    {
        _showWindowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowSignalName);
        _signalListenerCts = new CancellationTokenSource();
        var token = _signalListenerCts.Token;

        _ = Task.Run(
            () =>
            {
                while (!token.IsCancellationRequested)
                {
                    // Timed wait rather than an indefinite one, so exiting doesn't strand this thread.
                    if (_showWindowSignal.WaitOne(TimeSpan.FromMilliseconds(500)))
                    {
                        Dispatcher.Invoke(trayIconService.ShowMainWindow);
                    }
                }
            },
            token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _signalListenerCts?.Cancel();

        if (_host is not null)
        {
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            viewModel.SaveSettingsNow();

            _host.Services.GetRequiredService<ITrayIconService>().Dispose();
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        _signalListenerCts?.Dispose();
        _showWindowSignal?.Dispose();
        _instanceMutex?.Dispose();

        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IVirtualOutputDevice, VbCableOutputDevice>();
        services.AddSingleton<IVirtualCableDetector, VirtualCableDetector>();
        services.AddSingleton<IMicBoostEngine, MicBoostEngine>();
        services.AddSingleton<IAppAudioSessionService, AppAudioSessionService>();
        services.AddSingleton<IMediaSessionInfoService, MediaSessionInfoService>();
        services.AddSingleton<ISettingsService>(_ => new JsonSettingsService());

        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
