using NAudio.Dsp;
using NAudio.Wave;

namespace MicBoost.Audio.Dsp;

/// <summary>
/// Shapes the low end of the voice with a low-shelf filter (boosts/cuts everything below
/// ~200 Hz), independent of the overall gain stage. A 0 dB shelf is a unity passthrough.
/// </summary>
public sealed class BassShelfSampleProvider : ISampleProvider
{
    private const float CutoffHz = 200f;
    private const float ShelfSlope = 1.0f;

    private readonly ISampleProvider _source;
    private readonly object _filterLock = new();
    private BiQuadFilter[] _filters;
    private double _bassDb;

    public BassShelfSampleProvider(ISampleProvider source, double initialBassDb = BassMath.DefaultDb)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _bassDb = BassMath.DefaultDb;
        _filters = CreateFilters(BassMath.DefaultDb);
        BassDb = initialBassDb;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Current low-shelf gain in dB, clamped to [-10, +10]. Safe to set from any thread.</summary>
    public double BassDb
    {
        get => Volatile.Read(ref _bassDb);
        set
        {
            var clamped = BassMath.ClampDb(value);
            var newFilters = CreateFilters(clamped);

            lock (_filterLock)
            {
                _filters = newFilters;
            }

            Volatile.Write(ref _bassDb, clamped);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        var channels = _source.WaveFormat.Channels;

        BiQuadFilter[] filters;
        lock (_filterLock)
        {
            filters = _filters;
        }

        for (var i = 0; i < samplesRead; i++)
        {
            var channel = i % channels;
            buffer[offset + i] = filters[channel].Transform(buffer[offset + i]);
        }

        return samplesRead;
    }

    private BiQuadFilter[] CreateFilters(double bassDb)
    {
        var channels = _source.WaveFormat.Channels;
        var sampleRate = _source.WaveFormat.SampleRate;
        var filters = new BiQuadFilter[channels];

        for (var c = 0; c < channels; c++)
        {
            filters[c] = BiQuadFilter.LowShelf(sampleRate, CutoffHz, ShelfSlope, (float)bassDb);
        }

        return filters;
    }
}
