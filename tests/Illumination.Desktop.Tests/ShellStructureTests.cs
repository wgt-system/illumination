using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ShellStructureTests
{
    [Fact]
    public void Main_window_is_a_shell_with_explicit_page_views_and_study_default()
    {
        var root = FindRepositoryRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindow.axaml"));
        Assert.DoesNotContain("<TabControl", shell);
        Assert.Contains("IsVisible=\"{Binding IsStudyPage}\"", shell);
        Assert.Contains("<desktop:StudyView", shell);
        Assert.Contains("IsVisible=\"{Binding IsInsightsPage}\"", shell);
        Assert.Contains("<desktop:InsightsView", shell);
        Assert.Contains("<desktop:DecksView", shell);
        Assert.Contains("<desktop:LibraryView", shell);
        Assert.Contains("<desktop:ImportView", shell);
        Assert.True(shell.IndexOf("Study", StringComparison.Ordinal) < shell.IndexOf("Decks", StringComparison.Ordinal));

        var insights = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "InsightsView.axaml"));
        Assert.Contains("SuspendCommand", insights);
        Assert.Contains("ReactivateCommand", insights);
        Assert.Contains("MarkMasteredCommand", insights);
        Assert.Contains("UnmarkMasteredCommand", insights);
    }

    [Fact]
    public void Import_page_exposes_a_real_drop_surface()
    {
        var root = FindRepositoryRoot();
        var import = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "ImportView.axaml"));
        Assert.Contains("DragDrop.AllowDrop=\"True\"", import);
        Assert.Contains("DragDrop.Drop=\"OnBundleDrop\"", import);
        Assert.Contains("DragDrop.DragOver=\"OnBundleDragOver\"", import);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
