using MicBoost.Audio.Settings;

namespace MicBoost.Tests;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"micboost-tests-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var sut = new JsonSettingsService(_tempFile);

        var settings = sut.Load();

        Assert.Empty(settings.DeviceGainsDb);
        Assert.Null(settings.LastSelectedDeviceId);
        Assert.False(settings.LaunchOnStartup);
        Assert.True(settings.MinimizeToTray);
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.True(settings.AppAudioEnabled);
    }

    [Fact]
    public void Load_WhenFilePredatesTheAppAudioToggle_LeavesMirroringEnabled()
    {
        // Settings written before AppAudioEnabled existed have a chosen app but no flag;
        // they must keep mirroring rather than silently switching it off on upgrade.
        File.WriteAllText(_tempFile, """{"AppAudioProcessName":"spotify.exe"}""");
        var sut = new JsonSettingsService(_tempFile);

        var settings = sut.Load();

        Assert.True(settings.AppAudioEnabled);
        Assert.Equal("spotify.exe", settings.AppAudioProcessName);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var sut = new JsonSettingsService(_tempFile);
        var settings = new AppSettings
        {
            LastSelectedDeviceId = "device-123",
            LaunchOnStartup = true,
            MinimizeToTray = false,
            Theme = AppTheme.Light,
            AppAudioEnabled = false,
            AppAudioProcessName = "spotify.exe",
            AppAudioVolume = 0.65,
        };
        settings.SetGain("device-123", 4.5);
        settings.SetGain("device-456", -2.0);

        sut.Save(settings);
        var reloaded = sut.Load();

        Assert.Equal("device-123", reloaded.LastSelectedDeviceId);
        Assert.True(reloaded.LaunchOnStartup);
        Assert.False(reloaded.MinimizeToTray);
        Assert.Equal(AppTheme.Light, reloaded.Theme);
        Assert.Equal(4.5, reloaded.GetGainOrDefault("device-123"));
        Assert.Equal(-2.0, reloaded.GetGainOrDefault("device-456"));
        Assert.False(reloaded.AppAudioEnabled);
        Assert.Equal("spotify.exe", reloaded.AppAudioProcessName);
        Assert.Equal(0.65, reloaded.AppAudioVolume);
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"micboost-tests-{Guid.NewGuid():N}", "settings.json");
        var sut = new JsonSettingsService(nestedPath);

        sut.Save(new AppSettings());

        Assert.True(File.Exists(nestedPath));
        Directory.Delete(Path.GetDirectoryName(nestedPath)!, recursive: true);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ReturnsDefaultSettingsInsteadOfThrowing()
    {
        File.WriteAllText(_tempFile, "{ this is not valid json ");
        var sut = new JsonSettingsService(_tempFile);

        var settings = sut.Load();

        Assert.NotNull(settings);
        Assert.Empty(settings.DeviceGainsDb);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
