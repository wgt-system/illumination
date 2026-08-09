using Illumination.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Illumination.Infrastructure.Tests;

public class BootstrapTests
{
    [Fact]
    public void DbContext_can_be_constructed_with_sqlite_options_without_initializing_a_database()
    {
        var options = new DbContextOptionsBuilder<IlluminationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new IlluminationDbContext(options);

        Assert.Equal(6, context.Model.GetEntityTypes().Count());
    }

    [Fact]
    public void TimeProvider_can_be_controlled_in_tests_without_production_time_behavior()
    {
        var now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);

        Assert.Equal(now, timeProvider.GetUtcNow());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
