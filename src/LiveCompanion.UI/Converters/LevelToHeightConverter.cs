using System.Globalization;
using System.Windows.Data;

namespace LiveCompanion.UI.Converters;

/// <summary>
/// Convertit un niveau audio (float 0.0–1.0) en hauteur de barre VU-mètre.
/// La hauteur maximale par défaut est 60 pixels (passable via ConverterParameter).
/// </summary>
public sealed class LevelToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        float level = value is float f ? f : 0f;
        double maxHeight = parameter is string s && double.TryParse(s, CultureInfo.InvariantCulture, out double h) ? h : 60.0;
        return Math.Clamp(level, 0f, 1f) * maxHeight;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
