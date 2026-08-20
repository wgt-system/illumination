using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Illumination.Desktop;

public sealed class ZeroCountToBooleanConverter : IValueConverter
{
    public static ZeroCountToBooleanConverter Instance { get; } = new();

    private ZeroCountToBooleanConverter() { }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NonZeroCountToBooleanConverter : IValueConverter
{
    public static NonZeroCountToBooleanConverter Instance { get; } = new();

    private NonZeroCountToBooleanConverter() { }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class EnumerableIsEmptyConverter : IValueConverter
{
    public static EnumerableIsEmptyConverter Instance { get; } = new();

    private EnumerableIsEmptyConverter() { }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable values) return true;
        var enumerator = values.GetEnumerator();
        try { return !enumerator.MoveNext(); }
        finally { (enumerator as IDisposable)?.Dispose(); }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TopicLabelsToTextConverter : IValueConverter
{
    public static TopicLabelsToTextConverter Instance { get; } = new();

    private TopicLabelsToTextConverter() { }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<string> labels ? string.Join(", ", labels) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
