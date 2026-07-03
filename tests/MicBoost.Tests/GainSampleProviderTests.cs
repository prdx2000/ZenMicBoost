using MicBoost.Audio.Dsp;
using NAudio.Wave;

namespace MicBoost.Tests;

public class GainSampleProviderTests
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
    public void Read_AppliesLinearGainToEachSample()
    {
        var source = new ConstantSampleProvider(0.2f);
        var sut = new GainSampleProvider(source, initialGainDb: 6.0206); // linear gain = 2
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.Equal(0.4f, sample, precision: 3));
    }

    [Fact]
    public void Read_AtZeroDb_PassesSignalThroughUnchanged()
    {
        var source = new ConstantSampleProvider(0.3f);
        var sut = new GainSampleProvider(source, initialGainDb: 0.0);
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.Equal(0.3f, sample, precision: 5));
    }

    [Fact]
    public void Read_WhenGainWouldClip_LimiterKeepsOutputBelowFullScale()
    {
        var source = new ConstantSampleProvider(0.9f);
        var sut = new GainSampleProvider(source, initialGainDb: GainMath.MaxDb); // max gain, would clip hard without limiting
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        // The limiter's curve asymptotically approaches full scale but must never exceed it
        // (no wraparound/hard-clip artifacts), even when heavily overdriven.
        Assert.All(buffer, sample => Assert.True(sample <= 1.0f, $"Expected limited sample <= 1.0, got {sample}"));
        Assert.True(sut.IsLimiting);
    }

    [Fact]
    public void Read_ModeratelyOverThreshold_LimiterStaysStrictlyBelowFullScale()
    {
        var source = new ConstantSampleProvider(0.5f);
        var sut = new GainSampleProvider(source, initialGainDb: 6.0206); // linear gain = 2 -> raw 1.0, just over the 0.98 threshold
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.All(buffer, sample => Assert.True(sample is < 1.0f and > 0.98f, $"Expected softly limited sample, got {sample}"));
        Assert.True(sut.IsLimiting);
    }

    [Fact]
    public void Read_WhenSignalStaysUnderThreshold_LimiterDoesNotEngage()
    {
        var source = new ConstantSampleProvider(0.05f);
        var sut = new GainSampleProvider(source, initialGainDb: 0.0);
        var buffer = new float[4];

        sut.Read(buffer, 0, buffer.Length);

        Assert.False(sut.IsLimiting);
    }

    [Fact]
    public void GainDb_SetterClampsToSupportedRange()
    {
        var source = new ConstantSampleProvider(0.1f);
        var sut = new GainSampleProvider(source) { GainDb = 999.0 };

        Assert.Equal(GainMath.MaxDb, sut.GainDb);

        sut.GainDb = -999.0;
        Assert.Equal(GainMath.MinDb, sut.GainDb);
    }

    [Fact]
    public void Read_WhenMuted_OutputsSilenceButStillConsumesSource()
    {
        var source = new ConstantSampleProvider(0.5f);
        var sut = new GainSampleProvider(source, initialGainDb: 0.0) { IsMuted = true };
        var buffer = new float[4];

        var samplesRead = sut.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, samplesRead);
        Assert.All(buffer, sample => Assert.Equal(0.0f, sample));
    }
}
