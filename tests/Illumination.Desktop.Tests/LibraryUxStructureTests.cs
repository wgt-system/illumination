using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class LibraryUxStructureTests
{
    [Fact]
    public void Library_keeps_primary_authoring_separate_from_specialist_curation()
    {
        var root = FindRepositoryRoot();
        var library = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "LibraryView.axaml"));

        Assert.Contains("Find and edit individual cards here", library);
        Assert.Contains("Text=\"Review\"", library);
        Assert.DoesNotContain("Text=\"QA\"", library);
        Assert.Contains("Advanced: Review selection and quality filter", library);
        Assert.Contains("Advanced: quality, flags and review exchange", library);
        Assert.Contains("Specialist curation metadata", library);
        Assert.Contains("Optional ChatGPT-based review workflow", library);
        Assert.Contains("Question / task", library);
        Assert.Contains("Open Generate / Import", library);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
