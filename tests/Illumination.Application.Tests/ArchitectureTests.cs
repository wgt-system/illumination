using System.Reflection;
using Xunit;

namespace Illumination.Application.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Application_assembly_is_an_empty_boundary_without_infrastructure_or_presentation_references()
    {
        var assembly = Assembly.Load("Illumination.Application");
        var referencedAssemblies = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

        Assert.Equal("Illumination.Application", assembly.GetName().Name);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencedAssemblies);
        Assert.DoesNotContain("Avalonia", referencedAssemblies);
    }
}
