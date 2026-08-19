using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class CrossScreenEmptyStateTests
{
    [Fact]
    public void Decks_explains_absent_Decks_empty_membership_and_no_available_cards()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "DecksView.axaml"));

        Assert.Contains("No Decks yet", view);
        Assert.Contains("This Deck has no cards yet", view);
        Assert.Contains("No other Library cards are available to add", view);
        Assert.Contains("Open Generate / Import", view);
        Assert.Contains("ZeroCountToBooleanConverter", view);
        Assert.Contains("NonZeroCountToBooleanConverter", view);
    }

    [Fact]
    public void Library_distinguishes_empty_library_from_empty_filter_result()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "LibraryView.axaml"));

        Assert.Contains("No Learning Items yet", view);
        Assert.Contains("No cards match the current filters", view);
        Assert.Contains("Clear the filters or change the search", view);
        Assert.Contains("EnumerableIsEmptyConverter", view);
        Assert.Contains("New card", view);
        Assert.Contains("Open Generate / Import", view);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
