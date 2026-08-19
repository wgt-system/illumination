using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class StudyUxStructureTests
{
    [Fact]
    public void Study_surface_explains_scheduled_session_and_empty_states()
    {
        var root = FindRepositoryRoot();
        var study = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "StudyView.axaml"));

        Assert.Contains("Scheduled Study uses due and new cards", study);
        Assert.Contains("Session setup", study);
        Assert.Contains("Session active. Setup controls are locked", study);
        Assert.Contains("No scheduled Study Session is active", study);
        Assert.Contains("This session has no current card", study);
        Assert.Contains("Practice now", study);
        Assert.Contains("Session options", study);
        Assert.Contains("You always confirm the final grade", study);
        Assert.Contains("IsVisible=\"{Binding HasCurrentStudyItem}\"", study);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
