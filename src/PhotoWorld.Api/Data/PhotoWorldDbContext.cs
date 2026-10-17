using Microsoft.EntityFrameworkCore;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Data;

public class PhotoWorldDbContext(DbContextOptions<PhotoWorldDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<LogEntry> Logs => Set<LogEntry>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<PhotoLike> PhotoLikes => Set<PhotoLike>();
    public DbSet<PhotoComment> PhotoComments => Set<PhotoComment>();
    public DbSet<Report> Reports => Set<Report>();

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

        modelBuilder.Entity<Photo>(e =>
        {
            e.Property(p => p.Title).HasMaxLength(PhotoUploadLimits.MaxTitleLength);
            e.Property(p => p.Description).HasMaxLength(PhotoUploadLimits.MaxDescriptionLength);
            e.Property(p => p.DisplayPath).HasMaxLength(256);
            e.Property(p => p.ThumbnailPath).HasMaxLength(256);
            e.Property(p => p.CameraMake).HasMaxLength(100);
            e.Property(p => p.CameraModel).HasMaxLength(100);
            e.Property(p => p.Lens).HasMaxLength(150);
            e.Property(p => p.FocalLengthMm).HasPrecision(6, 1);
            e.Property(p => p.Aperture).HasPrecision(4, 1);
            e.Property(p => p.ExposureTime).HasMaxLength(20);
            e.HasOne(p => p.Owner).WithMany().HasForeignKey(p => p.OwnerId);
            e.HasIndex(p => p.CreatedAt).IsDescending();
            e.HasIndex(p => new { p.OwnerId, p.CreatedAt }).IsDescending(false, true);
            e.HasIndex(p => new { p.ShowOnHome, p.CreatedAt }).IsDescending(false, true);
        });

        modelBuilder.Entity<PhotoLike>(e =>
        {
            e.HasKey(l => new { l.PhotoId, l.UserId });
            e.HasOne(l => l.Photo).WithMany(p => p.Likes).HasForeignKey(l => l.PhotoId);
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId);
        });

        modelBuilder.Entity<PhotoComment>(e =>
        {
            e.Property(c => c.Text).HasMaxLength(PhotoUploadLimits.MaxCommentLength);
            e.HasOne(c => c.Photo).WithMany(p => p.Comments).HasForeignKey(c => c.PhotoId);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId);
            e.HasIndex(c => new { c.PhotoId, c.CreatedAt });
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_reports_one_target", "(photo_id is null) <> (comment_id is null)");
                t.HasCheckConstraint("ck_reports_status",
                    $"status in ('{ReportStatus.Open}', '{ReportStatus.Dismissed}', '{ReportStatus.Upheld}')");
                t.HasCheckConstraint("ck_reports_reason",
                    $"reason in ({string.Join(", ", ReportRules.Reasons.Keys.Select(k => $"'{k}'"))})");
            });
            e.Property(r => r.Reason).HasMaxLength(16);
            e.Property(r => r.Note).HasMaxLength(ReportRules.MaxNoteLength);
            e.Property(r => r.Status).HasMaxLength(16).HasDefaultValue(ReportStatus.Open);
            e.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId);
            // Reports go when their photo or comment goes (optional FKs would otherwise block the delete).
            e.HasOne(r => r.Photo).WithMany(p => p.Reports).HasForeignKey(r => r.PhotoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Comment).WithMany(c => c.Reports).HasForeignKey(r => r.CommentId).OnDelete(DeleteBehavior.Cascade);
            // One report per member per item. (Postgres treats NULLs as distinct, so each index only bites on its own target.)
            e.HasIndex(r => new { r.ReporterId, r.PhotoId }).IsUnique();
            e.HasIndex(r => new { r.ReporterId, r.CommentId }).IsUnique();
            e.HasIndex(r => new { r.PhotoId, r.Status });
            e.HasIndex(r => new { r.CommentId, r.Status });
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
