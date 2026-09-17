using Microsoft.EntityFrameworkCore;

namespace Aegis.Persistence;

public static class AegisDbContextFactory
{
    /// <summary>Opens (creating if needed) a SQLite-backed context at <paramref name="dbPath"/> — PostgreSQL is a documented future option, not yet wired up (US-017).</summary>
    public static AegisDbContext CreateSqlite(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AegisDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var context = new AegisDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
