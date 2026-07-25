using MicBoost.Audio.Dsp;
using MicBoost.Audio.Loopback;
using MicBoost.Audio.Output;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MicBoost.Audio.Engine;

/// <summary>
/// Wires WASAPI capture from the selected physical mic through a real-time gain/limiter
/// stage, optionally mixes in a mirrored app's audio, and renders the result into the
/// virtual cable's playback endpoint. Small buffers are used throughout to keep
/// conversational latency low.
/// </summary>
public sealed class MicBoostEngine : IMicBoostEngine
{
    private const int CaptureBufferMs = 20;
    private const int AppAudioBufferMs = 1000;

    // App-audio capture and the mic/render pipeline run off independent clocks, so the jitter
    // buffer between them needs a cushion. Connecting at the instant capture starts leaves it
    // near empty (measured: ~10ms held) and scheduling jitter drains it to zero, which
    // BufferedWaveProvider fills with silence: constant dropouts. Pre-rolling holds the level.
    private const int AppAudioPrerollMs = 150;

    /// <summary>
    /// The format everything is mixed in, independent of any one source's own format. Deriving
    /// it from the mic forces every other source through the mic's format, so a 96 kHz mono
    /// headset would collapse a mirrored app's 48 kHz stereo music to mono. 48 kHz stereo also
    /// matches what virtual cables typically expose, so the render stage rarely converts.
    /// </summary>
    private static readonly WaveFormat PipelineFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    private readonly IVirtualOutputDevice _virtualOutput;
    private readonly object _appAudioLock = new();

    private MMDevice? _captureDevice;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _buffer;
    private LevelMeterSampleProvider? _preMeter;
    private BassShelfSampleProvider? _bass;
    private GainSampleProvider? _gain;
    private MixingSampleProvider? _mixer;
    private SoftLimiterSampleProvider? _outputLimiter;
    private LevelMeterSampleProvider? _postMeter;

    private int? _appAudioProcessId;
    private double _appAudioVolume = 1.0;
    private IProcessLoopbackCapture? _appAudioCapture;
    private VolumeSampleProvider? _appAudioVolumeProvider;
    private bool _isAppAudioActive;

    public MicBoostEngine(IVirtualOutputDevice virtualOutput)
    {
        _virtualOutput = virtualOutput ?? throw new ArgumentNullException(nameof(virtualOutput));
    }

    public bool IsRunning => _capture is not null;

    public double GainDb
    {
        get => _gain?.GainDb ?? GainMath.DefaultDb;
        set
        {
            if (_gain is not null)
            {
                _gain.GainDb = value;
            }
        }
    }

    public double BassDb
    {
        get => _bass?.BassDb ?? BassMath.DefaultDb;
        set
        {
            if (_bass is not null)
            {
                _bass.BassDb = value;
            }
        }
    }

    public bool IsLimiting => (_gain?.IsLimiting ?? false) || (_outputLimiter?.IsLimiting ?? false);

    public bool IsMuted
    {
        get => _gain?.IsMuted ?? false;
        set
        {
            if (_gain is not null)
            {
                _gain.IsMuted = value;
            }
        }
    }

    public float InputLevel => _preMeter?.CurrentPeak ?? 0f;

    public float OutputLevel => _postMeter?.CurrentPeak ?? 0f;

    public event EventHandler<Exception>? EngineError;

    public int? AppAudioProcessId => _appAudioProcessId;

    public bool IsAppAudioActive => _isAppAudioActive;

    public double AppAudioVolume
    {
        get => _appAudioVolume;
        set
        {
            _appAudioVolume = Math.Clamp(value, 0d, 1d);
            if (_appAudioVolumeProvider is not null)
            {
                _appAudioVolumeProvider.Volume = (float)_appAudioVolume;
            }
        }
    }

    public event EventHandler<Exception>? AppAudioError;

    public void Start(string captureDeviceId, double initialGainDb)
    {
        Stop();

        using var enumerator = new MMDeviceEnumerator();
        _captureDevice = enumerator.GetDevice(captureDeviceId);

        _capture = new WasapiCapture(_captureDevice, true, CaptureBufferMs);
        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(500),
        };

        // Meter the mic at its own native format, then convert it into the pipeline format so
        // the DSP and mixing stages all agree regardless of what the mic reports.
        _preMeter = new LevelMeterSampleProvider(_buffer.ToSampleProvider());
        var micInPipelineFormat = SampleFormatAdapter.Adapt(_preMeter, PipelineFormat);
        _bass = new BassShelfSampleProvider(micInPipelineFormat);
        _gain = new GainSampleProvider(_bass, initialGainDb);

        // Mic audio is always mixer input #1; a mirrored app's audio (if selected) is added
        // as input #2 once its loopback capture connects. The limiter after the mixer guards
        // against two independently near-full-scale sources clipping when summed.
        _mixer = new MixingSampleProvider(PipelineFormat) { ReadFully = true };
        _mixer.AddMixerInput(_gain);
        _outputLimiter = new SoftLimiterSampleProvider(_mixer);
        _postMeter = new LevelMeterSampleProvider(_outputLimiter);

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        try
        {
            _virtualOutput.Start(_postMeter);
            _capture.StartRecording();
        }
        catch (Exception)
        {
            Stop();
            throw;
        }

        if (_appAudioProcessId is int processId)
        {
            _ = ReconnectAppAudioAsync(processId);
        }
    }

    public void Stop()
    {
        DetachAppAudio();

        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.StopRecording();
            _capture.Dispose();
            _capture = null;
        }

        _virtualOutput.Stop();

        _captureDevice?.Dispose();
        _captureDevice = null;

        _buffer = null;
        _preMeter = null;
        _bass = null;
        _gain = null;
        _mixer = null;
        _outputLimiter = null;
        _postMeter = null;
    }

    public async Task SetAppAudioProcessAsync(int? processId)
    {
        _appAudioProcessId = processId;

        if (processId is int pid && _mixer is not null)
        {
            await ReconnectAppAudioAsync(pid).ConfigureAwait(false);
        }
        else
        {
            DetachAppAudio();
        }
    }

    private async Task ReconnectAppAudioAsync(int processId)
    {
        DetachAppAudio();

        if (_mixer is null)
        {
            // Not running yet (no mic selected). Start() reconnects once it is.
            return;
        }

        // Capture directly in the pipeline format. For a typical app rendering 48 kHz stereo
        // this is its native format, so its audio reaches the mixer without any conversion.
        var format = PipelineFormat;
        var capture = new ProcessLoopbackCapture();
        var buffer = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(AppAudioBufferMs),
        };

        capture.DataAvailable += (_, e) => buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        capture.CaptureError += OnAppAudioCaptureError;

        try
        {
            await capture.StartAsync(processId, format).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            capture.CaptureError -= OnAppAudioCaptureError;
            capture.Dispose();
            AppAudioError?.Invoke(this, ex);
            return;
        }

        await WaitForPrerollAsync(buffer, format).ConfigureAwait(false);

        lock (_appAudioLock)
        {
            // The selection may have changed again (or the engine stopped) while connecting.
            if (_appAudioProcessId != processId || _mixer is null)
            {
                capture.CaptureError -= OnAppAudioCaptureError;
                capture.Dispose();
                return;
            }

            var volumeProvider = new VolumeSampleProvider(buffer.ToSampleProvider())
            {
                Volume = (float)_appAudioVolume,
            };

            _appAudioCapture = capture;
            _appAudioVolumeProvider = volumeProvider;
            _isAppAudioActive = true;
            _mixer.AddMixerInput(volumeProvider);
        }
    }

    private static async Task WaitForPrerollAsync(BufferedWaveProvider buffer, WaveFormat format)
    {
        var targetBytes = (int)(format.AverageBytesPerSecond * (AppAudioPrerollMs / 1000.0));
        var deadline = DateTime.UtcNow.AddSeconds(2);

        // A target that never fills means the app is rendering silence, for which WASAPI delivers
        // nothing at all. Give up after the deadline and connect anyway rather than hanging.
        while (buffer.BufferedBytes < targetBytes && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private void DetachAppAudio()
    {
        IProcessLoopbackCapture? captureToDispose;

        lock (_appAudioLock)
        {
            if (_appAudioVolumeProvider is not null)
            {
                _mixer?.RemoveMixerInput(_appAudioVolumeProvider);
            }

            captureToDispose = _appAudioCapture;
            _appAudioCapture = null;
            _appAudioVolumeProvider = null;
            _isAppAudioActive = false;
        }

        if (captureToDispose is not null)
        {
            captureToDispose.CaptureError -= OnAppAudioCaptureError;
            captureToDispose.Dispose();
        }
    }

    private void OnAppAudioCaptureError(object? sender, Exception ex)
    {
        DetachAppAudio();
        AppAudioError?.Invoke(this, ex);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
        => _buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            EngineError?.Invoke(this, e.Exception);
        }
    }

    public void Dispose()
    {
        Stop();
        _virtualOutput.Dispose();
    }
}
