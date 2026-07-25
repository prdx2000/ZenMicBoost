using MicBoost.Audio.Dsp;
using NAudio.Wave;

namespace MicBoost.Tests;

public class SampleFormatAdapterTests
{
    private sealed class ConstantSampleProvider : ISampleProvider
    {
        private readonly float _value;

        public ConstantSampleProvider(float value, int sampleRate, int channels)
        {
            _value = value;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
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

    private static readonly WaveFormat Pipeline = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    [Fact]
    public void Adapt_WhenSourceAlreadyMatchesTarget_ReturnsSourceUnchanged()
    {
        var source = new ConstantSampleProvider(0.5f, 48000, 2);

        var adapted = SampleFormatAdapter.Adapt(source, Pipeline);

        Assert.Same(source, adapted);
    }

    [Fact]
    public void Adapt_MonoSource_ProducesStereo()
    {
        var source = new ConstantSampleProvider(0.5f, 48000, 1);

        var adapted = SampleFormatAdapter.Adapt(source, Pipeline);

        Assert.Equal(2, adapted.WaveFormat.Channels);
        Assert.Equal(48000, adapted.WaveFormat.SampleRate);
    }

    [Fact]
    public void Adapt_DifferentSampleRate_ProducesTargetRate()
    {
        var source = new ConstantSampleProvider(0.5f, 96000, 2);

        var adapted = SampleFormatAdapter.Adapt(source, Pipeline);

        Assert.Equal(48000, adapted.WaveFormat.SampleRate);
        Assert.Equal(2, adapted.WaveFormat.Channels);
    }

    [Fact]
    public void Adapt_MonoAtDifferentRate_ConvertsBothRateAndChannels()
    {
        // The real-world case that caused hollow-sounding output: a 96 kHz mono headset mic
        // feeding a 48 kHz stereo pipeline.
        var source = new ConstantSampleProvider(0.5f, 96000, 1);

        var adapted = SampleFormatAdapter.Adapt(source, Pipeline);

        Assert.Equal(48000, adapted.WaveFormat.SampleRate);
        Assert.Equal(2, adapted.WaveFormat.Channels);
    }

    [Fact]
    public void Adapt_StereoSourceToMonoTarget_ProducesMono()
    {
        var source = new ConstantSampleProvider(0.5f, 48000, 2);
        var monoTarget = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

        var adapted = SampleFormatAdapter.Adapt(source, monoTarget);

        Assert.Equal(1, adapted.WaveFormat.Channels);
    }

    [Fact]
    public void Adapt_MonoToStereo_CarriesSignalOnBothChannels()
    {
        var source = new ConstantSampleProvider(0.5f, 48000, 1);

        var adapted = SampleFormatAdapter.Adapt(source, Pipeline);
        var buffer = new float[64];
        var read = adapted.Read(buffer, 0, buffer.Length);

        Assert.True(read > 0);
        // Both interleaved channels should carry the signal, not just the left.
        Assert.All(buffer.Take(read), sample => Assert.Equal(0.5f, sample, precision: 4));
    }
}
