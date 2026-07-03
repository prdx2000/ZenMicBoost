using System.Globalization;
using System.Windows.Data;

namespace MicBoost.App.Converters;

/// <summary>Formats a dB value as e.g. "+3.5 dB" or "-2.0 dB", always showing the sign.</summary>
public sealed class GainDbDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double db)
        {
            return string.Empty;
        }

        var sign = db > 0 ? "+" : string.Empty;
        return $"{sign}{db.ToString("0.0", CultureInfo.InvariantCulture)} dB";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
