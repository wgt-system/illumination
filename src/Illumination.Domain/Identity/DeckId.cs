namespace Illumination.Domain.Identity;

public readonly record struct DeckId
{
    public DeckId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A Deck ID must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static DeckId New() => new(Guid.NewGuid());

    public static DeckId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
