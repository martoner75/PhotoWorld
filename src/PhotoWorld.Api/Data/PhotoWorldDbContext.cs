using Microsoft.EntityFrameworkCore;

namespace PhotoWorld.Api.Data;

public class PhotoWorldDbContext(DbContextOptions<PhotoWorldDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();

    /// <summary>
    /// PhotoWorld's own schema in the Supabase database. Keeping it out of <c>public</c> means
    /// Supabase's auto-generated Data API (PostgREST) doesn't expose these tables to the anon key.
    /// </summary>
    public const string Schema = "photoworld";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(200);
            e.Property(u => u.Email).HasMaxLength(320);
            e.Property(u => u.AvatarUrl).HasMaxLength(2048);
            e.HasIndex(u => u.Email);
        });

        modelBuilder.Entity<ExternalLogin>(e =>
        {
            e.Property(l => l.Provider).HasMaxLength(32);
            e.Property(l => l.ProviderUserId).HasMaxLength(128);
            e.HasIndex(l => new { l.Provider, l.ProviderUserId }).IsUnique();
            e.HasOne(l => l.User).WithMany(u => u.ExternalLogins).HasForeignKey(l => l.UserId);
        });

        modelBuilder.Entity<LogEntry>(e =>
        {
            e.ToTable("app_logs");
            e.Property(l => l.Level).HasMaxLength(16);
            e.Property(l => l.Category).HasMaxLength(256);
            e.Property(l => l.EventName).HasMaxLength(256);
            e.Property(l => l.Application).HasMaxLength(64);
            e.Property(l => l.Environment).HasMaxLength(32);
            e.Property(l => l.MachineName).HasMaxLength(128);
            e.Property(l => l.TraceId).HasMaxLength(32);
            e.Property(l => l.SpanId).HasMaxLength(16);
            e.Property(l => l.Properties).HasColumnType("jsonb");
            e.HasIndex(l => l.Timestamp).IsDescending();
            e.HasIndex(l => new { l.Level, l.Timestamp });
            e.HasIndex(l => l.TraceId);
        });
    }
}
