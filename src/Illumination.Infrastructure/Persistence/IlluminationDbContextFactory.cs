using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Illumination.Infrastructure.Persistence;

public sealed class IlluminationDbContextFactory : IDesignTimeDbContextFactory<IlluminationDbContext>
{
    public IlluminationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IlluminationDbContext>()
            .UseSqlite("Data Source=illumination.db")
            .Options;
        return new IlluminationDbContext(options);
    }
}
