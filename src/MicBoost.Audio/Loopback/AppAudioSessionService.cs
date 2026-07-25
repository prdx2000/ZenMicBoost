using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MicBoost.Audio.Loopback;

/// <summary>
/// Reads the default playback device's audio session list to find processes that could be
/// mirrored. Sessions live on whichever endpoint the process is actually rendering to, so
/// this only sees apps currently routed through the system's default output.
/// </summary>
public sealed class AppAudioSessionService : IAppAudioSessionService
{
    public IReadOnlyList<ProcessAudioSessionInfo> GetActiveSessions()
    {
        var result = new List<ProcessAudioSessionInfo>();
        var seenProcessIds = new HashSet<int>();

        using var enumerator = new MMDeviceEnumerator();
        MMDevice? device;
        try
        {
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception)
        {
            // No default playback device (e.g. all output devices disabled) — nothing to list.
            return result;
        }

        using (device)
        {
            var sessions = device.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                using var session = sessions[i];

                if (session.IsSystemSoundsSession || session.State == AudioSessionState.AudioSessionStateExpired)
                {
                    continue;
                }

                var processId = (int)session.GetProcessID;
                if (processId == 0 || processId == Environment.ProcessId || !seenProcessIds.Add(processId))
                {
                    continue;
                }

                var info = TryDescribeProcess(processId);
                if (info is not null)
                {
                    result.Add(info);
                }
            }
        }

        return result;
    }

    private static ProcessAudioSessionInfo? TryDescribeProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var displayName = TryGetFriendlyDescription(process) ?? process.ProcessName;
            return new ProcessAudioSessionInfo(processId, process.ProcessName, displayName);
        }
        catch (Exception)
        {
            // Process exited between enumeration and lookup — just skip it.
            return null;
        }
    }

    private static string? TryGetFriendlyDescription(Process process)
    {
        try
        {
            var description = process.MainModule?.FileVersionInfo.FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }
        catch (Exception)
        {
            // Denied for elevated/protected processes, or a bitness mismatch — fall back to the process name.
            return null;
        }
    }
}
