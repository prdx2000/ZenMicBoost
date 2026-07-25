using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MicBoost.Audio.Dsp;

/// <summary>
/// Adapts an arbitrary sample stream to a target sample rate and channel count at runtime.
///
/// Sources in this app rarely agree on a format: a headset mic might capture 96 kHz mono while
/// the app being mirrored renders 48 kHz stereo. Forcing every source into whichever format one
/// of them happens to use collapses stereo music to mono, so each source is converted into a
/// common pipeline format through here instead.
/// </summary>
public static class SampleFormatAdapter
{
    /// <summary>
    /// Wraps <paramref name="source"/> in whatever resampling/channel conversion is needed to
    /// produce <paramref name="target"/>'s rate and channel count. Returns the source unchanged
    /// when it already matches, so a matching source costs nothing.
    /// </summary>
    public static ISampleProvider Adapt(ISampleProvider source, WaveFormat target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var result = source;

        // Resample before any mono->stereo upmix, so the (more expensive) resampling runs over
        // one channel rather than two identical ones.
        if (result.WaveFormat.SampleRate != target.SampleRate)
        {
            result = new WdlResamplingSampleProvider(result, target.SampleRate);
        }

        if (result.WaveFormat.Channels != target.Channels)
        {
            result = ConvertChannels(result, target.Channels);
        }

        return result;
    }

    private static ISampleProvider ConvertChannels(ISampleProvider source, int targetChannels)
    {
        var sourceChannels = source.WaveFormat.Channels;

        return (sourceChannels, targetChannels) switch
        {
            (1, 2) => new MonoToStereoSampleProvider(source),
            (2, 1) => new StereoToMonoSampleProvider(source),

            // Anything more exotic (surround capture, etc.): map the first target-many channels
            // straight through, which keeps the front pair for the usual 5.1/7.1 case.
            _ => new MultiplexingSampleProvider(new[] { source }, targetChannels),
        };
    }
}
