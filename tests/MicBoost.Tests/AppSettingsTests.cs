using MicBoost.Audio.Dsp;
using MicBoost.Audio.Settings;

namespace MicBoost.Tests;

public class AppSettingsTests
{
    [Fact]
    public void GetGainOrDefault_UnknownDevice_ReturnsDefaultGain()
    {
        var settings = new AppSettings();

        Assert.Equal(GainMath.DefaultDb, settings.GetGainOrDefault("unknown-device"));
    }

    [Fact]
    public void SetGain_ThenGetGainOrDefault_ReturnsStoredValue()
    {
        var settings = new AppSettings();

        settings.SetGain("device-1", 7.5);

        Assert.Equal(7.5, settings.GetGainOrDefault("device-1"));
    }

    [Fact]
    public void SetGain_ClampsOutOfRangeValues()
    {
        var settings = new AppSettings();

        settings.SetGain("device-1", 999.0);
        settings.SetGain("device-2", -999.0);

        Assert.Equal(GainMath.MaxDb, settings.GetGainOrDefault("device-1"));
        Assert.Equal(GainMath.MinDb, settings.GetGainOrDefault("device-2"));
    }

    [Fact]
    public void SetGain_IsIndependentPerDevice()
    {
        var settings = new AppSettings();

        settings.SetGain("device-1", 5.0);
        settings.SetGain("device-2", -5.0);

        Assert.Equal(5.0, settings.GetGainOrDefault("device-1"));
        Assert.Equal(-5.0, settings.GetGainOrDefault("device-2"));
    }
}
