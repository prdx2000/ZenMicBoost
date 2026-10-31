using System.Runtime.InteropServices;

namespace MicBoost.Audio.Engine;

/// <summary>
/// Keeps the real-time audio path on schedule when Windows decides MicBoost isn't important.
/// Once the window is hidden (tray) or the displays go to sleep, Windows 11 power-throttles the
/// process (EcoQoS: efficiency cores, lowered clocks) and ignores its timer resolution. With
/// 20ms buffers the audio threads then miss WASAPI deadlines, every underrun is filled with
/// silence, and the voice comes out choppy and robotic.
/// </summary>
internal static class AudioScheduling
{
    private const int ProcessPowerThrottling = 4;
    private const uint PowerThrottlingCurrentVersion = 1;
    private const uint PowerThrottlingExecutionSpeed = 0x1;
    private const uint PowerThrottlingIgnoreTimerResolution = 0x4;

    [ThreadStatic]
    private static bool t_isProAudioThread;

    /// <summary>
    /// Opts the whole process out of power throttling, whether or not its window is visible.
    /// </summary>
    public static void DisableProcessPowerThrottling()
    {
        var state = new ProcessPowerThrottlingState
        {
            Version = PowerThrottlingCurrentVersion,
            ControlMask = PowerThrottlingExecutionSpeed | PowerThrottlingIgnoreTimerResolution,
            // Controlled but not set: throttling is explicitly off rather than left to Windows.
            StateMask = 0,
        };

        // Best effort: older Windows builds reject the timer-resolution flag, and audio still
        // works without it, just with less headroom.
        NativeMethods.SetProcessInformation(
            NativeMethods.GetCurrentProcess(),
            ProcessPowerThrottling,
            ref state,
            (uint)Marshal.SizeOf<ProcessPowerThrottlingState>());
    }

    /// <summary>
    /// Registers the calling thread with MMCSS as "Pro Audio" the first time it's called on that
    /// thread. Meant for threads we don't own (NAudio's capture/render threads), from inside
    /// their callbacks; the registration is released when the thread exits.
    /// </summary>
    public static void EnsureCurrentThreadIsProAudio()
    {
        if (t_isProAudioThread)
        {
            return;
        }

        t_isProAudioThread = true;
        NativeMethods.AvSetMmThreadCharacteristics("Pro Audio", out _);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessInformation(
            IntPtr hProcess,
            int processInformationClass,
            ref ProcessPowerThrottlingState processInformation,
            uint processInformationSize);

        [DllImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr AvSetMmThreadCharacteristics(string taskName, out int taskIndex);
    }
}
