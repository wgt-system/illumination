namespace Illumination.Application.ContentAcquisition;

public enum LanguageProficiencyLevel
{
    A1,
    A2,
    B1,
    B2,
    C1,
    C2,
}

public enum LanguageExerciseProfile
{
    VocabularyFlashcards,
    PhrasesAndChunks,
    Translation,
    GrammarPractice,
    Comprehension,
    MixedPractice,
}

public sealed record LanguageGenerationGuidance(
    string? InstructionLanguage = null,
    string? SourceLanguage = null,
    string? TargetLanguage = null,
    LanguageProficiencyLevel? ProficiencyLevel = null,
    LanguageExerciseProfile? ExerciseProfile = null,
    FollowUpProgressionMode? ProgressionMode = null,
    bool HasSourceDeckContext = false)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(InstructionLanguage) &&
        string.IsNullOrWhiteSpace(SourceLanguage) &&
        string.IsNullOrWhiteSpace(TargetLanguage) &&
        ProficiencyLevel is null &&
        ExerciseProfile is null;
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

        if (guidance.ProficiencyLevel is { } level)
        {
            lines.Add($"- Learner proficiency target: CEFR {level}. Treat this as a generation difficulty target, not a claim that generated content is formally CEFR-certified.");
            lines.Add($"- Keep vocabulary frequency, grammar, sentence length, idiomaticity, required background knowledge, and learner production demands coherent with {level}; do not drift down to repetitive first-lesson filler or jump to near-fluent production merely to make items harder.");
            lines.Add($"- Prefer a broad useful range inside {level}. Easier prerequisite material is acceptable only when it supports the requested learning goal; harder material must not exceed the progression rule below.");
            lines.Add($"- Progression rule: {ProgressionGuardrail(level, guidance.ProgressionMode)}");
        }

        if (guidance.ExerciseProfile is { } profile)
            lines.AddRange(ExerciseProfileGuidance(profile));

        if (guidance.HasSourceDeckContext)
        {
            lines.Add("- Treat the source Deck prompts and reference solutions included above as an explicit anti-duplication inventory. Do not generate the same word, phrase, question, answer pair, or a cosmetic near-duplicate again unless Reinforce intentionally targets weak material.");
            lines.Add("- When continuing or advancing, prefer genuinely new vocabulary/constructions that connect to the source Deck rather than recycling its easiest or most salient examples.");
        }

        lines.Add("If a language field is not specified, infer only what is safely implied by Subject and other user Guidance; do not invent an immersion setting.");

        return new GeneratedContentPrompt(prompt.Prompt.TrimEnd() + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static string ProgressionGuardrail(LanguageProficiencyLevel level, FollowUpProgressionMode? progression) => progression switch
    {
        FollowUpProgressionMode.Reinforce => $"stay within CEFR {level}, biasing toward clearer/scaffolded practice of weak material without collapsing the whole set to trivial beginner vocabulary.",
        FollowUpProgressionMode.Continue => $"stay within CEFR {level}; introduce new material at roughly the same proficiency band rather than silently escalating difficulty.",
        FollowUpProgressionMode.Advance => NextLevel(level) is { } next
            ? $"move gradually from CEFR {level} toward adjacent level {next}; do not skip proficiency bands or require substantially higher-level free production."
            : "remain within CEFR C2 while increasing breadth, nuance, register, or precision rather than inventing a level above C2.",
        _ => $"stay predominantly within CEFR {level}; do not make large unrequested difficulty jumps.",
    };

    private static LanguageProficiencyLevel? NextLevel(LanguageProficiencyLevel level) => level switch
    {
        LanguageProficiencyLevel.A1 => LanguageProficiencyLevel.A2,
        LanguageProficiencyLevel.A2 => LanguageProficiencyLevel.B1,
        LanguageProficiencyLevel.B1 => LanguageProficiencyLevel.B2,
        LanguageProficiencyLevel.B2 => LanguageProficiencyLevel.C1,
        LanguageProficiencyLevel.C1 => LanguageProficiencyLevel.C2,
        LanguageProficiencyLevel.C2 => null,
        _ => null,
    };

    private static IReadOnlyList<string> ExerciseProfileGuidance(LanguageExerciseProfile profile) => profile switch
    {
        LanguageExerciseProfile.VocabularyFlashcards =>
        [
            "- Exercise profile: vocabulary flashcards.",
            "- Generate real flashcard-like lexical material: normally one useful word, fixed expression, collocation, or short phrase per Learning Item with a concise counterpart/meaning in the requested direction.",
            "- Do not replace vocabulary cards with meta-questions, essays, cultural explanations, long reading tasks, or open-ended fluent production. Use concise self-assessed recall unless another explicitly allowed response mode is genuinely better.",
            "- Avoid overconcentrating on iconic first-lesson words when the requested level or source Deck indicates the learner is beyond them.",
        ],
        LanguageExerciseProfile.PhrasesAndChunks =>
        [
            "- Exercise profile: phrases and chunks.",
            "- Prefer common reusable multi-word expressions, collocations, formulaic chunks, and short practical utterances; keep each item independently reviewable and concise.",
        ],
        LanguageExerciseProfile.Translation =>
        [
            "- Exercise profile: translation practice.",
            "- Use short level-appropriate translation items with an explicit source → target direction and enough context to avoid false uniqueness or ambiguity.",
        ],
        LanguageExerciseProfile.GrammarPractice =>
        [
            "- Exercise profile: grammar practice.",
            "- Target grammar that is appropriate for the selected proficiency band through concise transformations, cloze/short-answer tasks, or focused recall; avoid turning every item into a grammar lecture.",
        ],
        LanguageExerciseProfile.Comprehension =>
        [
            "- Exercise profile: comprehension.",
            "- Use short level-appropriate input and focused comprehension checks. Keep passages and questions compact enough for repeated review rather than essay-style reading exercises.",
        ],
        LanguageExerciseProfile.MixedPractice =>
        [
            "- Exercise profile: mixed practice.",
            "- Mix useful vocabulary/phrases, focused translation, grammar, and comprehension only where each item remains concise and level-coherent; variety must not override quality or difficulty stability.",
        ],
        _ => [],
    };
}
