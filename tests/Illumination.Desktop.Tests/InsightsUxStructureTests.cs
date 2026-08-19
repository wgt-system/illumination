using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class InsightsUxStructureTests
{
    [Fact]
    public void Insights_prioritizes_learning_decisions_over_scheduler_and_admin_detail()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "InsightsView.axaml"));

        Assert.Contains("What needs attention", view);
        Assert.Contains("Deck focus", view);
        Assert.Contains("No learning history yet", view);
        Assert.Contains("No cards match the current Deck/filter selection.", view);
        Assert.Contains("Create learning-aware follow-up content", view);
        Assert.Contains("Prepare follow-up generation", view);
        Assert.Contains("Advanced scheduler details", view);
        Assert.Contains("Manage lifecycle", view);
        Assert.Contains("Recent history", view);
        Assert.Contains("Library state", view);
        Assert.DoesNotContain("Generate follow-up Deck", view);
    }

    [Fact]
    public void Insights_view_model_exposes_empty_and_selection_states_for_explanatory_ui()
    {
        var root = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "LearningInsightsViewModel.cs"));

        Assert.Contains("public bool HasLearningData", viewModel);
        Assert.Contains("public bool HasDecks", viewModel);
        Assert.Contains("public bool HasItems", viewModel);
        Assert.Contains("public bool HasSelectedItem", viewModel);
        Assert.Contains("public bool HasReviews", viewModel);
        Assert.Contains("public bool HasSessions", viewModel);
        Assert.Contains("CanExecute = nameof(CanGenerateFollowUp)", viewModel);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
