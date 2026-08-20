using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ShellUxStructureTests
{
    [Fact]
    public void Shell_keeps_primary_navigation_separate_from_local_data_maintenance()
    {
        var root = FindRepositoryRoot();
        var shell = File.ReadAllText(Path.Combine(root, "src", "Illumination.Desktop", "MainWindow.axaml"));

        Assert.Contains("Content=\"Study\"", shell);
        Assert.Contains("Content=\"Decks\"", shell);
        Assert.Contains("Content=\"Insights\"", shell);
        Assert.Contains("Content=\"Library\"", shell);
        Assert.Contains("Content=\"Generate / Import\"", shell);
        Assert.Contains("Advanced: local data", shell);
        Assert.Contains("Maintenance controls for the local SQLite database", shell);
        Assert.Equal(1, CountOccurrences(shell, "{Binding StatusMessage}"));
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
