namespace Illumination.Domain.Learning;

public sealed record AnswerChoice
{
    public AnswerChoice(string text, bool isCorrect = false)
    {
        DomainText.RequireNonWhitespace(text, nameof(text));
        Text = text;
        IsCorrect = isCorrect;
    }

    public string Text { get; }

    public bool IsCorrect { get; }
}
