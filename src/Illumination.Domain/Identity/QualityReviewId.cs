namespace Illumination.Domain.Identity;

public readonly record struct QualityReviewId
{
    public QualityReviewId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A Quality Review ID must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static QualityReviewId New() => new(Guid.NewGuid());

    public static QualityReviewId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
