namespace Illumination.Application.ContentAcquisition;

public sealed record LanguageGenerationGuidance(
    string? InstructionLanguage = null,
    string? SourceLanguage = null,
    string? TargetLanguage = null)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(InstructionLanguage) &&
        string.IsNullOrWhiteSpace(SourceLanguage) &&
        string.IsNullOrWhiteSpace(TargetLanguage);
}

public static class LanguageContentPromptGuidance
{
    private const string Marker = "Explicit language-learning controls:";

    public static GeneratedContentPrompt Apply(GeneratedContentPrompt prompt, LanguageGenerationGuidance guidance)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(guidance);
        if (guidance.IsEmpty || prompt.Prompt.Contains(Marker, StringComparison.Ordinal)) return prompt;

        var lines = new List<string>
        {
            Marker,
            "These controls are explicit user guidance and override generic language-direction assumptions without weakening Content Bundle 1.0 requirements.",
        };
        if (!string.IsNullOrWhiteSpace(guidance.InstructionLanguage))
            lines.Add($"- Instruction/meta language: {guidance.InstructionLanguage!.Trim()}. Keep task instructions and explanations in this language unless the task itself explicitly requires otherwise.");
        if (!string.IsNullOrWhiteSpace(guidance.SourceLanguage))
            lines.Add($"- Source/input language: {guidance.SourceLanguage!.Trim()}. Material presented for translation or transformation should use this language where applicable.");
        if (!string.IsNullOrWhiteSpace(guidance.TargetLanguage))
            lines.Add($"- Target/answer language: {guidance.TargetLanguage!.Trim()}. Expected learner production should use this language where applicable.");
        if (!string.IsNullOrWhiteSpace(guidance.SourceLanguage) && !string.IsNullOrWhiteSpace(guidance.TargetLanguage))
            lines.Add($"- Translation direction is explicitly {guidance.SourceLanguage!.Trim()} → {guidance.TargetLanguage!.Trim()}; state that direction unambiguously in every translation task.");
        lines.Add("If a language field is not specified, infer only what is safely implied by Subject and other user Guidance; do not invent an immersion setting.");

        return new GeneratedContentPrompt(prompt.Prompt.TrimEnd() + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
