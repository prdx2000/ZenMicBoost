using System.Windows;
using MicBoost.App.Services;
using MicBoost.App.ViewModels;
using MicBoost.App.Views;
using MicBoost.Audio.Devices;
using MicBoost.Audio.Engine;
using MicBoost.Audio.Output;
using MicBoost.Audio.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MicBoost.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            viewModel.SaveSettingsNow();

            _host.Services.GetRequiredService<ITrayIconService>().Dispose();
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IVirtualOutputDevice, VbCableOutputDevice>();
        services.AddSingleton<IVirtualCableDetector, VirtualCableDetector>();
        services.AddSingleton<IMicBoostEngine, MicBoostEngine>();
        services.AddSingleton<ISettingsService>(_ => new JsonSettingsService());

        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
