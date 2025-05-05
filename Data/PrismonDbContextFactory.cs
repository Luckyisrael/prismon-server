using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Prismon.Api.Data;

namespace Prismon.Api.Data;

public class PrismonDbContextFactory : IDesignTimeDbContextFactory<PrismonDbContext>
{
    public PrismonDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PrismonDbContext>();
        optionsBuilder.UseSqlite("Data Source=prismon.db"); // Match appsettings.json

        return new PrismonDbContext(optionsBuilder.Options);
    }
}