namespace Illumination.Domain.Learning;

public sealed record Hint
{
    public Hint(string text)
    {
        DomainText.RequireNonWhitespace(text, nameof(text));
        Text = text;
    }

    public string Text { get; }
}
