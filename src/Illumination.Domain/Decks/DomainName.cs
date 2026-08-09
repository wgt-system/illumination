namespace Illumination.Domain.Decks;

internal static class DomainName
{
    public static void RequireNonWhitespace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Name must not be null, empty, or whitespace.", parameterName);
        }
    }
}
