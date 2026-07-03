using MicBoost.Audio.Dsp;

namespace MicBoost.Tests;

public class BassMathTests
{
    [Theory]
    [InlineData(45.0, 30.0)]
    [InlineData(-45.0, -30.0)]
    [InlineData(15.0, 15.0)]
    [InlineData(30.0, 30.0)]
    [InlineData(-30.0, -30.0)]
    public void ClampDb_ClampsToSupportedRange(double input, double expected)
    {
        Assert.Equal(expected, BassMath.ClampDb(input));
    }

    [Fact]
    public void DefaultDb_IsZero() => Assert.Equal(0.0, BassMath.DefaultDb);
}
