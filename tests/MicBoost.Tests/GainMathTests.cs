using MicBoost.Audio.Dsp;

namespace MicBoost.Tests;

public class GainMathTests
{
    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(6.0206, 2.0)]
    [InlineData(-6.0206, 0.5)]
    [InlineData(20.0, 10.0)]
    public void DbToLinear_MatchesKnownValues(double db, double expectedLinear)
    {
        var actual = GainMath.DbToLinear(db);
        Assert.Equal(expectedLinear, actual, precision: 3);
    }

    [Theory]
    [InlineData(1.0, 0.0)]
    [InlineData(2.0, 6.0206)]
    [InlineData(0.5, -6.0206)]
    [InlineData(10.0, 20.0)]
    public void LinearToDb_MatchesKnownValues(double linear, double expectedDb)
    {
        var actual = GainMath.LinearToDb(linear);
        Assert.Equal(expectedDb, actual, precision: 3);
    }

    [Fact]
    public void LinearToDb_NonPositiveInput_ReturnsNegativeInfinity()
    {
        Assert.Equal(double.NegativeInfinity, GainMath.LinearToDb(0.0));
        Assert.Equal(double.NegativeInfinity, GainMath.LinearToDb(-1.0));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(3.5)]
    [InlineData(-3.5)]
    [InlineData(10.0)]
    [InlineData(-10.0)]
    public void DbToLinear_ThenLinearToDb_RoundTrips(double db)
    {
        var linear = GainMath.DbToLinear(db);
        var roundTripped = GainMath.LinearToDb(linear);
        Assert.Equal(db, roundTripped, precision: 9);
    }

    [Theory]
    [InlineData(45.0, 30.0)]
    [InlineData(-45.0, -30.0)]
    [InlineData(15.0, 15.0)]
    [InlineData(30.0, 30.0)]
    [InlineData(-30.0, -30.0)]
    public void ClampDb_ClampsToSupportedRange(double input, double expected)
    {
        Assert.Equal(expected, GainMath.ClampDb(input));
    }

    [Theory]
    [InlineData(0.24, 0.0)]
    [InlineData(0.26, 0.5)]
    [InlineData(0.74, 0.5)]
    [InlineData(0.76, 1.0)]
    [InlineData(-0.26, -0.5)]
    public void SnapToStep_RoundsToNearestHalfDb(double input, double expected)
    {
        Assert.Equal(expected, GainMath.SnapToStep(input), precision: 6);
    }

    [Fact]
    public void DefaultDb_IsZero() => Assert.Equal(0.0, GainMath.DefaultDb);
}
