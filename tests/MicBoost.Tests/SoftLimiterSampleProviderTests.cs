using MicBoost.Audio.Dsp;
using NAudio.Wave;

namespace MicBoost.Tests;

public class SoftLimiterSampleProviderTests
{
    private sealed class ConstantSampleProvider : ISampleProvider
    {
        private readonly float _value;

        public ConstantSampleProvider(float value, WaveFormat? waveFormat = null)
        {
            _value = value;
            WaveFormat = waveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = _value;
            }

            return count;
        }
    }

    [Fact]
    public void Read_WhenSignalStaysUnderThreshold_PassesThroughUnchanged()
    {
        var source = new ConstantSampleProvider(0.3f);
        var sut = new SoftLimiterSampleProvider(source);
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.Equal(0.3f, sample, precision: 5));
        Assert.False(sut.IsLimiting);
    }

    [Fact]
    public void Read_WhenTwoMixedSourcesWouldExceedFullScale_LimiterKeepsOutputBelowFullScale()
    {
        // Simulates mic + app audio summed together (e.g. 0.7 + 0.7 = 1.4), which neither
        // branch's own limiter would have caught individually.
        var source = new ConstantSampleProvider(1.4f);
        var sut = new SoftLimiterSampleProvider(source);
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.True(sample <= 1.0f, $"Expected limited sample <= 1.0, got {sample}"));
        Assert.True(sut.IsLimiting);
    }

    [Fact]
    public void Read_NegativeOverThreshold_LimitsSymmetrically()
    {
        var source = new ConstantSampleProvider(-1.4f);
        var sut = new SoftLimiterSampleProvider(source);
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.True(sample >= -1.0f, $"Expected limited sample >= -1.0, got {sample}"));
        Assert.True(sut.IsLimiting);
    }
}
