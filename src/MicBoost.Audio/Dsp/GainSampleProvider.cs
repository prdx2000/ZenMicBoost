using NAudio.Wave;

namespace MicBoost.Audio.Dsp;

/// <summary>
/// Applies a real-time dB gain to a 32-bit float sample stream, with a soft-knee
/// limiter above -0.18 dBFS so pushing gain hard never produces harsh digital clipping.
/// </summary>
public sealed class GainSampleProvider : ISampleProvider
{
    private const float LimiterThreshold = 0.98f;

    private readonly ISampleProvider _source;
    private double _gainDb;
    private float _linearGain;
    private volatile bool _isLimiting;

    public GainSampleProvider(ISampleProvider source, double initialGainDb = GainMath.DefaultDb)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        GainDb = initialGainDb;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>Current gain in dB, clamped to [-30, +30]. Safe to set from any thread.</summary>
    public double GainDb
    {
        get => Volatile.Read(ref _gainDb);
        set
        {
            var clamped = GainMath.ClampDb(value);
            Volatile.Write(ref _gainDb, clamped);
            Volatile.Write(ref _linearGain, (float)GainMath.DbToLinear(clamped));
        }
    }

    /// <summary>True if the limiter clamped any sample in the most recently processed block.</summary>
    public bool IsLimiting => _isLimiting;

    /// <summary>When true, output is silenced while still consuming the source stream. Safe to set from any thread.</summary>
    public bool IsMuted { get; set; }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        if (IsMuted)
        {
            Array.Clear(buffer, offset, samplesRead);
            _isLimiting = false;
            return samplesRead;
        }

        float gain = Volatile.Read(ref _linearGain);
        bool limitingThisBlock = false;

        for (int i = 0; i < samplesRead; i++)
        {
            float sample = buffer[offset + i] * gain;
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
