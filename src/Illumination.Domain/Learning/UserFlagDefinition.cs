using Illumination.Domain.Identity;

namespace Illumination.Domain.Learning;

public sealed class UserFlagDefinition
{
    private UserFlagDefinition(UserFlagDefinitionId id, string name, string meaning)
    {
        DomainText.RequireNonWhitespace(name, nameof(name));
        DomainText.RequireNonWhitespace(meaning, nameof(meaning));
        Id = id;
        Name = name;
        Meaning = meaning;
    }

    public UserFlagDefinitionId Id { get; }

    public string Name { get; }

    public string Meaning { get; }

    public static UserFlagDefinition Create(string name, string meaning) =>
        Create(UserFlagDefinitionId.New(), name, meaning);

    public static UserFlagDefinition Create(UserFlagDefinitionId id, string name, string meaning) =>
        new(id, name, meaning);
}
