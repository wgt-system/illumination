using Microsoft.EntityFrameworkCore;

namespace Illumination.Infrastructure.Persistence;

public class IlluminationDbContext(DbContextOptions<IlluminationDbContext> options) : DbContext(options)
{
}
