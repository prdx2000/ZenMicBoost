using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MicBoost.App.Converters;

/// <summary>True -&gt; Collapsed, False -&gt; Visible. Used to show the setup screen only when something is missing.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
