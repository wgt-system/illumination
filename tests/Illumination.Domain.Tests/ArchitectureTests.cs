using System.Reflection;
using Xunit;

namespace Illumination.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_assembly_has_no_infrastructure_or_presentation_references()
    {
        var assembly = Assembly.Load("Illumination.Domain");
        var referencedAssemblies = assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.DoesNotContain("Avalonia", referencedAssemblies);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencedAssemblies);
        Assert.DoesNotContain("CommunityToolkit.Mvvm", referencedAssemblies);
    }
}
