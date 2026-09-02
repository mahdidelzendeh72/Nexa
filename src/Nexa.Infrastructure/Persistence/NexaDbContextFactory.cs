using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexa.Infrastructure.Persistence;

public sealed class NexaDbContextFactory : IDesignTimeDbContextFactory<NexaDbContext>
{
    public NexaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NexaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=nexa;Username=nexa;Password=nexa")
            .Options;

        return new NexaDbContext(options);
    }
}
