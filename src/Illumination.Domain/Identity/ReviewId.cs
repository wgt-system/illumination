namespace Illumination.Domain.Identity;

public readonly record struct ReviewId
{
    public ReviewId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A Review ID must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ReviewId New() => new(Guid.NewGuid());

    public static ReviewId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
