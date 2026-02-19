using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LiveCompanion.App.Converters;

/// <summary>Returns true when the value is not null.</summary>
[ValueConversion(typeof(object), typeof(bool))]
public sealed class NotNullConverter : IValueConverter
{
    public static readonly NotNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Returns Visible when the value is not null, Collapsed otherwise.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NotNullToVisibleConverter : IValueConverter
{
    public static readonly NotNullToVisibleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Returns Visible when bool is true, Collapsed otherwise.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibleConverter : IValueConverter
{
    public static readonly BoolToVisibleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Returns Collapsed when bool is true, Visible otherwise.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToCollapsedConverter : IValueConverter
{
    public static readonly BoolToCollapsedConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
