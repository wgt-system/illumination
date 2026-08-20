using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class DecksUxStructureTests
{
    [Fact]
    public void Decks_prioritizes_learning_and_hides_maintenance_detail()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "DecksView.axaml"));

        Assert.Contains("Use selected Deck", view);
        Assert.Contains("Study selected", view);
        Assert.Contains("Practice now", view);
        Assert.Contains("Generate learning-aware follow-up", view);
        Assert.Contains("Manage selected Deck", view);
        Assert.Contains("Advanced: learning-state maintenance", view);
        Assert.Contains("Manage cards in this Deck", view);
        Assert.Contains("Advanced: selected-card learning state", view);
        Assert.Contains("Deleting a Deck removes only the Deck and memberships", view);
        Assert.DoesNotContain("Text=\"Deck management\"", view);
        Assert.DoesNotContain("Text=\"Available Learning Items\"", view);
    }

    [Fact]
    public void Deck_classification_keeps_topics_and_learning_profiles_distinct_and_profiles_composable()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "DecksView.axaml"));

        Assert.Contains("Text=\"Topics\"", view);
        Assert.Contains("Text=\"Learning profiles\"", view);
        Assert.Contains("General recall", view);
        Assert.Contains("Language learning", view);
        Assert.Contains("Coding / problem solving", view);
        Assert.Contains("Geospatial", view);
        Assert.Contains("ToggleSelectedDeckLearningActivityProfileCommand", view);
        Assert.Contains("separate from subject topics and from observed learning evidence", view);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
