using Microsoft.EntityFrameworkCore;

namespace PhotoWorld.Api.Data;

public class PhotoWorldDbContext(DbContextOptions<PhotoWorldDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
