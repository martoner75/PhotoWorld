using Microsoft.EntityFrameworkCore;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Api.Services.Activities;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Admin;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Notifications;
using PhotoWorld.Shared.Places;

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
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<BlogPostPhoto> BlogPostPhotos => Set<BlogPostPhoto>();
    public DbSet<BlogAttachment> BlogAttachments => Set<BlogAttachment>();
    public DbSet<BlogComment> BlogComments => Set<BlogComment>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<EmailSubscription> EmailSubscriptions => Set<EmailSubscription>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivitySettings> ActivitySettings => Set<ActivitySettings>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<ChallengeEntry> ChallengeEntries => Set<ChallengeEntry>();

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
            e.Property(u => u.SuspensionReason).HasMaxLength(AdminLimits.SuspensionReasonMax);
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
            e.HasOne(p => p.Place).WithMany().HasForeignKey(p => p.PlaceId).OnDelete(DeleteBehavior.SetNull);
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
                t.HasCheckConstraint("ck_reports_one_target", "num_nonnulls(photo_id, comment_id, blog_post_id, blog_comment_id) = 1");
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
            e.HasOne(r => r.BlogPost).WithMany(p => p.Reports).HasForeignKey(r => r.BlogPostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.BlogComment).WithMany(c => c.Reports).HasForeignKey(r => r.BlogCommentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.ReporterId, r.PhotoId }).IsUnique();
            e.HasIndex(r => new { r.ReporterId, r.CommentId }).IsUnique();
            e.HasIndex(r => new { r.ReporterId, r.BlogPostId }).IsUnique();
            e.HasIndex(r => new { r.ReporterId, r.BlogCommentId }).IsUnique();
            e.HasIndex(r => new { r.PhotoId, r.Status });
            e.HasIndex(r => new { r.CommentId, r.Status });
            e.HasIndex(r => new { r.BlogPostId, r.Status });
            e.HasIndex(r => new { r.BlogCommentId, r.Status });
        });

        modelBuilder.Entity<Blog>(e =>
        {
            e.HasKey(b => b.UserId);
            e.Property(b => b.Title).HasMaxLength(BlogLimits.BlogTitleMax);
            e.Property(b => b.Intro).HasMaxLength(BlogLimits.IntroMax);
            e.HasOne(b => b.User).WithOne().HasForeignKey<Blog>(b => b.UserId);
        });

        modelBuilder.Entity<BlogPost>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("ck_blog_posts_status",
                $"status in ('{BlogPostStatus.Draft}', '{BlogPostStatus.Published}')"));
            e.Property(p => p.Title).HasMaxLength(BlogLimits.PostTitleMax);
            e.Property(p => p.BodyHtml).HasMaxLength(BlogLimits.BodyMax);
            e.Property(p => p.Excerpt).HasMaxLength(BlogLimits.ExcerptLength + 10);
            e.Property(p => p.Status).HasMaxLength(16);
            e.HasOne(p => p.Author).WithMany().HasForeignKey(p => p.AuthorId);
            e.HasOne(p => p.CoverPhoto).WithMany().HasForeignKey(p => p.CoverPhotoId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(p => new { p.Status, p.PublishedAt }).IsDescending(false, true);
            e.HasIndex(p => new { p.AuthorId, p.UpdatedAt }).IsDescending(false, true);
        });

        modelBuilder.Entity<BlogPostPhoto>(e =>
        {
            e.HasKey(p => new { p.PostId, p.PhotoId });
            e.HasOne(p => p.Post).WithMany(p => p.Photos).HasForeignKey(p => p.PostId);
            e.HasOne(p => p.Photo).WithMany().HasForeignKey(p => p.PhotoId);
        });

        modelBuilder.Entity<BlogAttachment>(e =>
        {
            e.Property(a => a.FileName).HasMaxLength(BlogLimits.AttachmentFileNameMax);
            e.Property(a => a.StoragePath).HasMaxLength(256);
            e.HasOne(a => a.Post).WithMany(p => p.Attachments).HasForeignKey(a => a.PostId);
            e.HasIndex(a => new { a.PostId, a.CreatedAt });
        });

        modelBuilder.Entity<BlogComment>(e =>
        {
            e.Property(c => c.Text).HasMaxLength(BlogLimits.CommentMax);
            e.HasOne(c => c.Post).WithMany(p => p.Comments).HasForeignKey(c => c.PostId);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId);
            e.HasIndex(c => new { c.PostId, c.CreatedAt });
        });

        modelBuilder.Entity<Place>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_places_latitude", "latitude between -90 and 90");
                t.HasCheckConstraint("ck_places_longitude", "longitude between -180 and 180");
            });
            e.Property(p => p.Name).HasMaxLength(PlaceLimits.NameMax);
            e.Property(p => p.Note).HasMaxLength(PlaceLimits.NoteMax);
            // Goes with the account (cascade), like everything else a member creates.
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId);
            e.HasIndex(p => new { p.UserId, p.CreatedAt });
        });

        modelBuilder.Entity<EmailSubscription>(e =>
        {
            e.HasKey(s => s.UserId);
            e.ToTable(t => t.HasCheckConstraint("ck_email_subscriptions_frequency",
                $"frequency in ({string.Join(", ", NotificationFrequency.Labels.Keys.Select(k => $"'{k}'"))})"));
            e.Property(s => s.Email).HasMaxLength(NotificationLimits.EmailMax);
            e.Property(s => s.Frequency).HasMaxLength(16);
            e.Property(s => s.ConfirmTokenHash).HasMaxLength(64);
            e.Property(s => s.UnsubscribeToken).HasMaxLength(64);
            // Goes with the account (cascade), like everything else a member creates.
            e.HasOne(s => s.User).WithOne().HasForeignKey<EmailSubscription>(s => s.UserId);
            e.HasIndex(s => s.ConfirmTokenHash).IsUnique();
            e.HasIndex(s => s.UnsubscribeToken).IsUnique();
        });

        modelBuilder.Entity<Follow>(e =>
        {
            e.HasKey(f => new { f.FollowerId, f.FolloweeId });
            e.ToTable(t => t.HasCheckConstraint("ck_follows_not_self", "follower_id <> followee_id"));
            // Either account going takes the follow with it.
            e.HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerId);
            e.HasOne(f => f.Followee).WithMany().HasForeignKey(f => f.FolloweeId);
            e.HasIndex(f => f.FolloweeId);
        });

        modelBuilder.Entity<Block>(e =>
        {
            e.HasKey(b => new { b.BlockerId, b.BlockedId });
            e.ToTable(t => t.HasCheckConstraint("ck_blocks_not_self", "blocker_id <> blocked_id"));
            // Either account going takes the block with it.
            e.HasOne(b => b.Blocker).WithMany().HasForeignKey(b => b.BlockerId);
            e.HasOne(b => b.Blocked).WithMany(u => u.BlockedBy).HasForeignKey(b => b.BlockedId);
            e.HasIndex(b => b.BlockedId);
        });

        modelBuilder.Entity<Challenge>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("ck_challenges_dates", "ends_at > starts_at"));
            // Defaults so a challenge can be added with a plain SQL insert in the Supabase dashboard.
            e.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.CreatedAt).HasDefaultValueSql("now()");
            e.Property(c => c.Title).HasMaxLength(ChallengeLimits.TitleMax);
            e.Property(c => c.Description).HasMaxLength(ChallengeLimits.DescriptionMax);
            e.HasIndex(c => c.EndsAt).IsDescending();
        });

        modelBuilder.Entity<ChallengeEntry>(e =>
        {
            e.HasKey(x => new { x.ChallengeId, x.PhotoId });
            e.HasOne(x => x.Challenge).WithMany(c => c.Entries).HasForeignKey(x => x.ChallengeId);
            e.HasOne(x => x.Photo).WithMany().HasForeignKey(x => x.PhotoId);
            e.HasIndex(x => x.PhotoId);
        });

        modelBuilder.Entity<Activity>(e =>
        {
            e.ToTable("activities", t => t.HasCheckConstraint("ck_activities_type",
                $"type in ({string.Join(", ", ActivityTypes.All.Select(k => $"'{k}'"))})"));
            e.Property(a => a.Type).HasMaxLength(24);
            e.Property(a => a.Snippet).HasMaxLength(ActivityRules.SnippetMax + 1);
            // Everything cascades: an activity goes with its recipient, actor, photo, post or challenge.
            e.HasOne(a => a.Recipient).WithMany().HasForeignKey(a => a.RecipientId);
            e.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Photo).WithMany().HasForeignKey(a => a.PhotoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Post).WithMany().HasForeignKey(a => a.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.Challenge).WithMany().HasForeignKey(a => a.ChallengeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.RecipientId, a.CreatedAt }).IsDescending(false, true);
            e.HasIndex(a => new { a.RecipientId, a.ReadAt });
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<ActivitySettings>(e =>
        {
            e.HasKey(s => s.UserId);
            e.HasOne(s => s.User).WithOne().HasForeignKey<ActivitySettings>(s => s.UserId);
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
