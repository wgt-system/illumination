using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ShellUxStructureTests
{
    [Fact]
    public void Product_surface_keeps_primary_navigation_separate_from_local_data_maintenance()
    {
        var root = FindRepositoryRoot();
        var surface = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "IlluminationProductSurface.axaml"));

        Assert.Contains("Content=\"Study\"", surface);
        Assert.Contains("Content=\"Decks\"", surface);
        Assert.Contains("Content=\"Insights\"", surface);
        Assert.Contains("Content=\"Library\"", surface);
        Assert.Contains("Content=\"Generate / Import\"", surface);
        Assert.Contains("Advanced: local data", surface);
        Assert.Contains("Maintenance controls for the local SQLite database", surface);
        Assert.Equal(1, CountOccurrences(surface, "{Binding StatusMessage}"));
    }

    [Fact]
    public void Standalone_window_is_only_a_host_for_the_provider_surface()
    {
        var root = FindRepositoryRoot();
        var window = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindow.axaml"));

        Assert.Contains("<desktop:IlluminationProductSurface", window);
        Assert.DoesNotContain("Content=\"Study\"", window);
        Assert.DoesNotContain("Advanced: local data", window);
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Illumination.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the Illumination repository root.");
    }
}
