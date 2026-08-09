namespace Illumination.Domain.Learning;

public sealed record ReferenceSolution
{
    public ReferenceSolution(string content)
    {
        DomainText.RequireNonWhitespace(content, nameof(content));
        Content = content;
    }

    public string Content { get; }
}
