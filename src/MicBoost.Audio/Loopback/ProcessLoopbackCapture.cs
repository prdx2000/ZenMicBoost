using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace MicBoost.Audio.Loopback;

/// <summary>
/// Activates and reads from the WASAPI "process loopback" virtual audio device
/// (<c>VAD\Process_Loopback</c>), available since Windows 10 build 19041 (2004). Unlike
/// <see cref="NAudio.Wave.WasapiLoopbackCapture"/>, which captures a whole physical render
/// endpoint, this captures only the audio rendered by one process and its child process
/// tree. Selecting Chrome's main process therefore also picks up audio from its
/// renderer/GPU child processes, which is where a played-back tab's audio comes from.
///
/// NAudio 2.3.0 already ships the low-level COM interop pieces this needs
/// (<see cref="IActivateAudioInterfaceCompletionHandler"/>, <see cref="AudioClient"/>'s
/// public <c>IAudioClient</c> constructor) but doesn't yet expose a public API for
/// building the process-loopback activation params, so that part is done here directly.
/// </summary>
public sealed class ProcessLoopbackCapture : IProcessLoopbackCapture
{
    private const string VirtualAudioDeviceProcessLoopback = @"VAD\Process_Loopback";
    private const int AudioClientActivationTypeProcessLoopback = 1;
    private const int ProcessLoopbackModeIncludeTargetProcessTree = 0;
    private const ushort VT_BLOB = 65;

    // 200ms of headroom in WASAPI's engine-side buffer, on top of the jitter buffer this class
    // feeds downstream. This thread isn't scheduled as reliably as the mic's WasapiCapture thread
    // (see MMCSS registration below), and latency matters less for background app audio than for
    // live voice, so the extra buffer is cheap insurance against dropped packets.
    private const long BufferDurationHns = 2_000_000;

    /// <summary>True on Windows 10 2004 (build 19041) or later, where process-loopback capture exists.</summary>
    public static bool IsSupported => Environment.OSVersion.Version.Build >= 19041;

    private AudioClient? _audioClient;
    private AudioCaptureClient? _captureClient;
    private AutoResetEvent? _bufferReadyEvent;
    private Thread? _captureThread;
    private volatile bool _stopRequested;
    private WaveFormat? _waveFormat;

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<Exception>? CaptureError;

    public async Task StartAsync(int processId, WaveFormat format)
    {
        Stop();

        if (!IsSupported)
        {
            throw new PlatformNotSupportedException(
                "Per-app audio mirroring requires Windows 10 version 2004 (build 19041) or later.");
        }

        _waveFormat = format;
        var rawClient = await ActivateAsync(processId).ConfigureAwait(false);
        _audioClient = new AudioClient(rawClient);

        // The process-loopback virtual device is technically a render endpoint under the hood;
        // AUDCLNT_STREAMFLAGS_LOOPBACK is what makes WASAPI hand back capture semantics instead
        // of AUDCLNT_E_INVALID_STREAM_FLAG. This matches Microsoft's own loopback capture sample.
        _audioClient.Initialize(
            AudioClientShareMode.Shared,
            AudioClientStreamFlags.Loopback | AudioClientStreamFlags.EventCallback,
            BufferDurationHns,
            0,
            format,
            Guid.Empty);

        _bufferReadyEvent = new AutoResetEvent(false);
        _audioClient.SetEventHandle(_bufferReadyEvent.SafeWaitHandle.DangerousGetHandle());
        _captureClient = _audioClient.AudioCaptureClient;

        _stopRequested = false;
        _captureThread = new Thread(CaptureThreadProc)
        {
            IsBackground = true,
            Name = "MicBoost-AppAudioCapture",
        };
        _captureThread.Start();
    }

    public void Stop()
    {
        _stopRequested = true;
        _bufferReadyEvent?.Set();

        // A CaptureError subscriber can call back into Stop() from the capture thread itself,
        // mid-exception, before that thread's run loop returns. Joining would self-deadlock,
        // and the thread is already winding down, so skip it.
        if (_captureThread is not null && Thread.CurrentThread != _captureThread)
        {
            _captureThread.Join(1000);
        }

        _captureThread = null;

        _captureClient = null;

        _audioClient?.Dispose();
        _audioClient = null;

        _bufferReadyEvent?.Dispose();
        _bufferReadyEvent = null;
    }

    private void CaptureThreadProc()
    {
        // Without an MMCSS boost this thread competes for CPU on equal footing with everything
        // else, and under real load it gets delayed past WASAPI's buffer deadline, which sounds
        // like glitchy audio. The mic's WasapiCapture gets an equivalent boost internally.
        var mmcssHandle = NativeMethods.AvSetMmThreadCharacteristics("Pro Audio", out _);

        try
        {
            _audioClient!.Start();
        }
        catch (Exception ex)
        {
            CaptureError?.Invoke(this, ex);
            RevertMmcss(mmcssHandle);
            return;
        }

        try
        {
            while (!_stopRequested)
            {
                _bufferReadyEvent!.WaitOne(200);
                if (_stopRequested)
                {
                    break;
                }

                ReadAvailablePackets();
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested)
            {
                CaptureError?.Invoke(this, ex);
            }
        }
        finally
        {
            try
            {
                _audioClient?.Stop();
            }
            catch (Exception)
            {
                // Already torn down (e.g. target process exited).
            }

            RevertMmcss(mmcssHandle);
        }
    }

    private static void RevertMmcss(IntPtr mmcssHandle)
    {
        if (mmcssHandle != IntPtr.Zero)
        {
            NativeMethods.AvRevertMmThreadCharacteristics(mmcssHandle);
        }
    }

    private void ReadAvailablePackets()
    {
        var blockAlign = _waveFormat!.BlockAlign;

        while (_captureClient!.GetNextPacketSize() > 0)
        {
            var bufferPtr = _captureClient.GetBuffer(out var framesAvailable, out var flags);
            var byteCount = framesAvailable * blockAlign;
            var managedBuffer = new byte[byteCount];

            if ((flags & AudioClientBufferFlags.Silent) == 0 && byteCount > 0)
            {
                Marshal.Copy(bufferPtr, managedBuffer, 0, byteCount);
            }

            _captureClient.ReleaseBuffer(framesAvailable);
            DataAvailable?.Invoke(this, new WaveInEventArgs(managedBuffer, byteCount));
        }
    }

    private static async Task<IAudioClient> ActivateAsync(int processId)
    {
        var activationParams = new AudioClientActivationParams
        {
            ActivationType = AudioClientActivationTypeProcessLoopback,
            ProcessLoopbackParams = new AudioClientProcessLoopbackParams
            {
                TargetProcessId = (uint)processId,
                ProcessLoopbackMode = ProcessLoopbackModeIncludeTargetProcessTree,
            },
        };

        var paramsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<AudioClientActivationParams>());
        var propVariantPtr = Marshal.AllocHGlobal(Marshal.SizeOf<PropVariant>());

        try
        {
            Marshal.StructureToPtr(activationParams, paramsPtr, false);

            var propVariant = new PropVariant
            {
                Vt = VT_BLOB,
                BlobSize = (uint)Marshal.SizeOf<AudioClientActivationParams>(),
                BlobData = paramsPtr,
            };
            Marshal.StructureToPtr(propVariant, propVariantPtr, false);

            var handler = new ActivationCompletionHandler();
            var riid = typeof(IAudioClient).GUID;

            var hr = NativeMethods.ActivateAudioInterfaceAsync(
                VirtualAudioDeviceProcessLoopback, riid, propVariantPtr, handler, out _);

            if (hr != 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            return await handler.Completion.ConfigureAwait(false);
        }
        finally
        {
            Marshal.FreeHGlobal(paramsPtr);
            Marshal.FreeHGlobal(propVariantPtr);
        }
    }

    public void Dispose() => Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientProcessLoopbackParams
    {
        public uint TargetProcessId;
        public uint ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientActivationParams
    {
        public int ActivationType;
        public AudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    // Matches the real Win32 PROPVARIANT layout (confirmed against NAudio's own PropVariant,
    // which reports a managed size of 24 bytes with the union starting at offset 8): a 2-byte
    // vt tag, 6 bytes of reserved padding, then (for VT_BLOB) a 4-byte size and 8-byte pointer.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort Vt;

        [FieldOffset(8)]
        public uint BlobSize;

        [FieldOffset(16)]
        public IntPtr BlobData;
    }

    private sealed class ActivationCompletionHandler : IActivateAudioInterfaceCompletionHandler
    {
        private readonly TaskCompletionSource<IAudioClient> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IAudioClient> Completion => _tcs.Task;

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
        {
            try
            {
                activateOperation.GetActivateResult(out var hr, out var activatedInterface);
                if (hr != 0)
                {
                    _tcs.TrySetException(Marshal.GetExceptionForHR(hr) ?? new COMException("Activation failed", hr));
                    return;
                }

                _tcs.TrySetResult((IAudioClient)activatedInterface);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("Mmdevapi.dll", ExactSpelling = true)]
        public static extern int ActivateAudioInterfaceAsync(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            IntPtr activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);

        [DllImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr AvSetMmThreadCharacteristics(string taskName, out int taskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AvRevertMmThreadCharacteristics(IntPtr avrtHandle);
    }
}
