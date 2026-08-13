namespace Illumination.Domain.Identity;

public readonly record struct UserFlagDefinitionId
{
    public UserFlagDefinitionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A User Flag Definition ID must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static UserFlagDefinitionId New() => new(Guid.NewGuid());

    public static UserFlagDefinitionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
