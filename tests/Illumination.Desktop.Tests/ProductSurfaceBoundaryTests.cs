using Avalonia.Controls;
using Illumination.Desktop;
using Xunit;

namespace Illumination.Desktop.Tests;

public sealed class ProductSurfaceBoundaryTests
{
    [Fact]
    public void Product_surface_boundary_is_public_and_control_based()
    {
        Assert.True(typeof(IlluminationProductSurface).IsPublic);
        Assert.True(typeof(IlluminationProductSurfaceFactory).IsPublic);

        var create = typeof(IlluminationProductSurfaceFactory).GetMethod(
            nameof(IlluminationProductSurfaceFactory.CreateAsync),
            Type.EmptyTypes);

        Assert.NotNull(create);
        Assert.True(create!.IsPublic && create.IsStatic);
        Assert.Equal(typeof(Task<Control>), create.ReturnType);
    }
}
