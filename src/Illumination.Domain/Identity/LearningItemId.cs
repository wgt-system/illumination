namespace Illumination.Domain.Identity;

public readonly record struct LearningItemId
{
    public LearningItemId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A Learning Item ID must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static LearningItemId New() => new(Guid.NewGuid());

    public static LearningItemId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
