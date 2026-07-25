using MicBoost.Audio.Dsp;
using NAudio.Wave;

namespace MicBoost.Tests;

public class BassShelfSampleProviderTests
{
    private sealed class SineSampleProvider : ISampleProvider
    {
        private readonly float _frequency;
        private readonly int _sampleRate;
        private double _phase;

        public SineSampleProvider(float frequency, int sampleRate = 48000)
        {
            _frequency = frequency;
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = (float)Math.Sin(_phase);
                _phase += 2 * Math.PI * _frequency / _sampleRate;
            }

            return count;
        }
    }

    private static double Rms(float[] buffer)
    {
        double sumSquares = 0;
        foreach (var sample in buffer)
        {
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / buffer.Length);
    }

    /// <summary>Reads and discards enough samples for the filter's transient to settle, then measures RMS.</summary>
    private static double MeasureSteadyStateRms(ISampleProvider provider, int settleSamples, int measureSamples)
    {
        var settle = new float[settleSamples];
        provider.Read(settle, 0, settleSamples);

        var measure = new float[measureSamples];
        provider.Read(measure, 0, measureSamples);
        return Rms(measure);
    }

    [Fact]
    public void BassDb_AtZero_SignalPassesThroughApproximatelyUnchanged()
    {
        var source = new SineSampleProvider(80f);
        var sut = new BassShelfSampleProvider(source, initialBassDb: 0.0);

        var rms = MeasureSteadyStateRms(sut, settleSamples: 1000, measureSamples: 4000);

        // A unit-amplitude sine has RMS = 1/sqrt(2) ~= 0.7071; a 0 dB shelf is unity gain.
        Assert.InRange(rms, 0.69, 0.72);
    }

    [Fact]
    public void BassDb_BoostedLowFrequency_IncreasesOutputLevel()
    {
        var flatSut = new BassShelfSampleProvider(new SineSampleProvider(80f), initialBassDb: 0.0);
        var flatRms = MeasureSteadyStateRms(flatSut, 1000, 4000);

        var boostedSut = new BassShelfSampleProvider(new SineSampleProvider(80f), initialBassDb: BassMath.MaxDb);
        var boostedRms = MeasureSteadyStateRms(boostedSut, 1000, 4000);

        // Deep in the shelf band (well below the ~200 Hz cutoff), gain approaches the configured
        // linear gain at max boost, so assert it's substantially louder.
        var ratio = boostedRms / flatRms;
        Assert.True(ratio > 2.5, $"Expected boosted low-frequency RMS to be notably louder, ratio was {ratio}");
    }

    [Fact]
    public void BassDb_BoostedHighFrequency_LeavesOutputLevelMostlyUnchanged()
    {
        var flatSut = new BassShelfSampleProvider(new SineSampleProvider(5000f), initialBassDb: 0.0);
        var flatRms = MeasureSteadyStateRms(flatSut, 1000, 4000);

        var boostedSut = new BassShelfSampleProvider(new SineSampleProvider(5000f), initialBassDb: BassMath.MaxDb);
        var boostedRms = MeasureSteadyStateRms(boostedSut, 1000, 4000);

        // Well above the shelf's cutoff, the bass control shouldn't meaningfully touch the signal.
        var ratio = boostedRms / flatRms;
        Assert.True(ratio < 1.3, $"Expected high-frequency RMS to stay close to unchanged, ratio was {ratio}");
    }

    [Fact]
    public void BassDb_SetterClampsToSupportedRange()
    {
        var sut = new BassShelfSampleProvider(new SineSampleProvider(80f)) { BassDb = 999.0 };

        Assert.Equal(BassMath.MaxDb, sut.BassDb);

        sut.BassDb = -999.0;
        Assert.Equal(BassMath.MinDb, sut.BassDb);
    }
}
