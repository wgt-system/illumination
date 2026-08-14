namespace Illumination.Domain.Learning;

public sealed record AnswerChoice
{
    public AnswerChoice(string text, bool isCorrect = false, string? id = null)
    {
        DomainText.RequireNonWhitespace(text, nameof(text));
        Text = text;
        IsCorrect = isCorrect;
        Id = id ?? string.Empty;
    }

    public string Id { get; }

    public string Text { get; }

    public bool IsCorrect { get; }
}
