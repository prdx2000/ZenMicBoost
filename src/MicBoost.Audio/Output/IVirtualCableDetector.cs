namespace MicBoost.Audio.Output;

public sealed record VirtualCableStatus(bool IsInstalled, string? RenderDeviceName, string? CaptureDeviceName)
{
    public static VirtualCableStatus NotInstalled { get; } = new(false, null, null);
}

/// <summary>Detects whether a compatible virtual audio cable driver is installed.</summary>
public interface IVirtualCableDetector
{
    VirtualCableStatus Detect();
}
