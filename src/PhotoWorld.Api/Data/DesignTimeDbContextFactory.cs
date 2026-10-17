using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoWorld.Api.Data;

/// <summary>
/// Used by <c>dotnet ef</c>. Reads <c>ConnectionStrings:PhotoWorld</c> from the API's user-secrets locally
/// or the <c>ConnectionStrings__PhotoWorld</c> environment variable in CI, so <c>database update</c>
/// targets Supabase. <c>migrations add</c> doesn't connect, so it also works with neither set.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PhotoWorldDbContext>
{
    public PhotoWorldDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration.GetConnectionString("PhotoWorld");

        return new(new DbContextOptionsBuilder<PhotoWorldDbContext>()
            .UseNpgsql(string.IsNullOrWhiteSpace(connectionString) ? "Host=localhost;Database=photoworld" : connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PhotoWorldDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options);
    }
}
