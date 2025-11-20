using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MesMiddleware.Monitor.Data;

/// <summary>
/// Design-time factory for EF Core migrations
/// </summary>
public class MonitorDbContextFactory : IDesignTimeDbContextFactory<MonitorDbContext>
{
    public MonitorDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MonitorDbContext>();
        optionsBuilder.UseSqlite("Data Source=monitor.db");

        return new MonitorDbContext(optionsBuilder.Options);
    }
}
