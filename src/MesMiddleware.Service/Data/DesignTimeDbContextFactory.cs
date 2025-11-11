using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MesMiddleware.Service.Data;

/// <summary>
/// Design-time factory for creating MiddlewareDbContext during EF Core migrations.
/// Only used by EF Core tools (dotnet ef migrations), not at runtime.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MiddlewareDbContext>
{
    public MiddlewareDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MiddlewareDbContext>();

        // Use a temp database path for migrations
        optionsBuilder.UseSqlite("Data Source=Data/queue.db");

        return new MiddlewareDbContext(optionsBuilder.Options);
    }
}
