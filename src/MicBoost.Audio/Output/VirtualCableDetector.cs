using NAudio.CoreAudioApi;

namespace MicBoost.Audio.Output;

public sealed class VirtualCableDetector : IVirtualCableDetector
{
    public VirtualCableStatus Detect()
    {
        using var enumerator = new MMDeviceEnumerator();

        using var render = VirtualCableLocator.FindRenderEndpoint(enumerator);
        using var capture = VirtualCableLocator.FindCaptureEndpoint(enumerator);

        if (render is null || capture is null)
        {
            return VirtualCableStatus.NotInstalled;
        }

        return new VirtualCableStatus(true, render.FriendlyName, capture.FriendlyName);
    }
}
