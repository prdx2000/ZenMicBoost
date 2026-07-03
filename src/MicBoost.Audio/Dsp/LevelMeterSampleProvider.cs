using NAudio.Wave;

namespace MicBoost.Audio.Dsp;

/// <summary>
/// Transparent passthrough that tracks the peak absolute sample value of the most
/// recently processed block, for driving a UI level meter. Chain one before and one
/// after <see cref="GainSampleProvider"/> to show before/after levels.
/// </summary>
public sealed class LevelMeterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private float _peak;

    public LevelMeterSampleProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Peak absolute sample value (typically 0..1) from the most recent block. Thread-safe to read.</summary>
    public float CurrentPeak => Volatile.Read(ref _peak);

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        float peak = 0f;

        for (int i = 0; i < samplesRead; i++)
        {
            float abs = MathF.Abs(buffer[offset + i]);
            if (abs > peak)
            {
                peak = abs;
            }
        }

        Volatile.Write(ref _peak, peak);
        return samplesRead;
    }
}
