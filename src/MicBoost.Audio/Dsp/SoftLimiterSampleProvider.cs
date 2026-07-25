using NAudio.Wave;

namespace MicBoost.Audio.Dsp;

/// <summary>
/// A unity-gain soft-knee limiter (tanh saturation above -0.18 dBFS, matching
/// <see cref="GainSampleProvider"/>'s limiter). Sits after the mic and app-audio branches are
/// mixed together, since two independently near-full-scale sources summed can exceed 0 dBFS
/// even when neither one does on its own.
/// </summary>
public sealed class SoftLimiterSampleProvider : ISampleProvider
{
    private const float LimiterThreshold = 0.98f;

    private readonly ISampleProvider _source;
    private volatile bool _isLimiting;

    public SoftLimiterSampleProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>True if the limiter clamped any sample in the most recently processed block.</summary>
    public bool IsLimiting => _isLimiting;

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        bool limitingThisBlock = false;

        for (int i = 0; i < samplesRead; i++)
        {
            float sample = buffer[offset + i];
            float abs = MathF.Abs(sample);

            if (abs > LimiterThreshold)
            {
                float sign = MathF.Sign(sample);
                float over = abs - LimiterThreshold;
                float headroom = 1f - LimiterThreshold;
                sample = sign * (LimiterThreshold + headroom * MathF.Tanh(over / headroom));
                limitingThisBlock = true;
            }

            buffer[offset + i] = sample;
        }

        _isLimiting = limitingThisBlock;
        return samplesRead;
    }
}
