using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoWorld.Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the model without a real Supabase connection.
/// Commands that touch the database (<c>database update</c>) take <c>--connection</c> instead.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PhotoWorldDbContext>
{
    public PhotoWorldDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PhotoWorldDbContext>()
        .UseNpgsql("Host=localhost;Database=photoworld", npgsql =>
            npgsql.MigrationsHistoryTable("__ef_migrations_history", PhotoWorldDbContext.Schema))
        .UseSnakeCaseNamingConvention()
        .Options);
}
