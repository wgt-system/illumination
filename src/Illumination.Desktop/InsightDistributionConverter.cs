using System.Globalization;
using Avalonia.Data.Converters;
using Illumination.Application.Insights;

namespace Illumination.Desktop;

public sealed class InsightDistributionConverter : IValueConverter
{
    public static InsightDistributionConverter Instance { get; } = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is AssessmentDistribution distribution ? InsightPresentationFormatter.Distribution(distribution) : string.Empty;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
