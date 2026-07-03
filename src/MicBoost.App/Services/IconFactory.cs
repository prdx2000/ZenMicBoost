using DrawingIcon = System.Drawing.Icon;
using WpfApplication = System.Windows.Application;

namespace MicBoost.App.Services;

/// <summary>Loads the app icon (embedded as a WPF resource) as a GDI+ icon for the tray NotifyIcon.</summary>
public static class IconFactory
{
    private static readonly Uri IconUri = new("pack://application:,,,/Resources/micboost.ico", UriKind.Absolute);

    public static DrawingIcon LoadTrayIcon()
    {
        var resourceInfo = WpfApplication.GetResourceStream(IconUri)
            ?? throw new InvalidOperationException("Embedded tray icon resource not found.");

        using var stream = resourceInfo.Stream;
        return new DrawingIcon(stream);
    }
}
