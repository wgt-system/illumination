namespace Illumination.Domain.Learning;

internal static class DomainText
{
    public static void RequireNonWhitespace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Text must not be null, empty, or whitespace.", parameterName);
        }
    }
}
