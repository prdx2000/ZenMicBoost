using MicBoost.Audio.Dsp;
using MicBoost.Audio.Output;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MicBoost.Audio.Engine;

/// <summary>
/// Wires WASAPI capture from the selected physical mic through a real-time gain/limiter
/// stage and into the virtual cable's playback endpoint. Small buffers are used throughout
/// to keep conversational latency low.
/// </summary>
public sealed class MicBoostEngine : IMicBoostEngine
{
    private const int CaptureBufferMs = 20;

    private readonly IVirtualOutputDevice _virtualOutput;

    private MMDevice? _captureDevice;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _buffer;
    private LevelMeterSampleProvider? _preMeter;
    private BassShelfSampleProvider? _bass;
    private GainSampleProvider? _gain;
    private LevelMeterSampleProvider? _postMeter;

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

    public bool IsLimiting => _gain?.IsLimiting ?? false;

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

        _preMeter = new LevelMeterSampleProvider(_buffer.ToSampleProvider());
        _bass = new BassShelfSampleProvider(_preMeter);
        _gain = new GainSampleProvider(_bass, initialGainDb);
        _postMeter = new LevelMeterSampleProvider(_gain);

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
    }

    public void Stop()
    {
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
        _postMeter = null;
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
