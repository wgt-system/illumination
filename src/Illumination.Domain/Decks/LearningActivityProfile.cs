namespace Illumination.Domain.Decks;

/// <summary>
/// Describes the kind of learning activity a Deck participates in.
/// This is explicit product configuration, not observed learner evidence and not a subject taxonomy.
/// Multiple profiles may apply to one Deck.
/// </summary>
public enum LearningActivityProfile
{
    GeneralRecall,
    LanguageLearning,
    CodingProblemSolving,
    Geospatial,
}
