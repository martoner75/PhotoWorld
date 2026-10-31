using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Api.Services;
using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared;

namespace PhotoWorld.Api.Controllers;

/// <summary>
/// "Download my data": a JSON file with what the signed-in member shared or set up, in plain terms. It never includes
/// secrets (sign-in tokens, email confirmation or unsubscribe tokens) or other members' private details; other members
/// appear by display name only.
/// </summary>
[ApiController]
[Authorize]
public class DataExportController(
    PhotoWorldDbContext db,
    SupabaseStorage storage,
    TimeProvider clock,
    ILogger<DataExportController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [HttpGet(ApiRoutes.MyDataExport)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (User.GetUserId() is not { } me) return Unauthorized();

        var profile = await db.Users.AsNoTracking().Where(u => u.Id == me)
            .Select(u => new
            {
                u.Id, u.DisplayName, u.Email, u.AvatarUrl, u.CreatedAt, u.LastLoginAt,
                SignInProviders = u.ExternalLogins.Select(l => l.Provider).ToList(),
            })
            .SingleOrDefaultAsync(ct);
        if (profile is null) return NotFound();

        var photos = (await db.Photos.AsNoTracking().Where(p => p.OwnerId == me).OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id, p.Title, p.Description, p.DisplayPath, p.Width, p.Height, p.CreatedAt, p.TakenAt,
                    p.CameraMake, p.CameraModel, p.Lens, p.FocalLengthMm, p.Aperture, p.ExposureTime, p.Iso,
                    p.ReviewStatus, p.WantsFeedback, p.AllowDownload, p.Watermarked, p.ShowOnHome,
                    Tags = p.Tags.Select(t => t.Name).ToList(),
                    Place = p.Place == null ? null : p.Place.Name,
                })
                .ToListAsync(ct))
            .Select(p => new
            {
                p.Id, p.Title, p.Description, ImageUrl = storage.PublicUrl(p.DisplayPath), p.Width, p.Height,
                UploadedAt = p.CreatedAt, p.TakenAt, p.CameraMake, p.CameraModel, p.Lens, p.FocalLengthMm, p.Aperture,
                p.ExposureTime, p.Iso, p.ReviewStatus, p.WantsFeedback, p.AllowDownload, p.Watermarked, p.ShowOnHome,
                p.Tags, p.Place,
            }).ToList();

        var export = new
        {
            ExportedAt = clock.GetUtcNow(),
            Note = "Everything you shared or set up on PhotoCommunity. Image links are public addresses of your photos.",
            Profile = profile,
            Photos = photos,
            PhotoComments = await db.PhotoComments.AsNoTracking().Where(c => c.AuthorId == me).OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.PhotoId, c.Text, c.CreatedAt }).ToListAsync(ct),
            PhotosYouLiked = await db.PhotoLikes.AsNoTracking().Where(l => l.UserId == me).OrderBy(l => l.CreatedAt)
                .Select(l => new { l.PhotoId, LikedAt = l.CreatedAt }).ToListAsync(ct),
            FeedbackYouGave = await db.PhotoFeedback.AsNoTracking().Where(f => f.AuthorId == me).OrderBy(f => f.CreatedAt)
                .Select(f => new { f.PhotoId, f.Composition, f.Light, f.Focus, f.Comment, f.CreatedAt, f.UpdatedAt }).ToListAsync(ct),
            Blog = await db.Blogs.AsNoTracking().Where(b => b.UserId == me)
                .Select(b => new { b.Title, b.Intro, b.CreatedAt, b.UpdatedAt }).SingleOrDefaultAsync(ct),
            BlogPosts = await db.BlogPosts.AsNoTracking().Where(p => p.AuthorId == me).OrderBy(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id, p.Title, p.BodyHtml, p.Status, p.CreatedAt, p.UpdatedAt, p.PublishedAt,
                    Attachments = p.Attachments.Select(a => a.FileName).ToList(),
                }).ToListAsync(ct),
            BlogComments = await db.BlogComments.AsNoTracking().Where(c => c.AuthorId == me).OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.PostId, c.Text, c.CreatedAt }).ToListAsync(ct),
            Collections = await db.Collections.AsNoTracking().Where(c => c.OwnerId == me).OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.Title, c.Description, c.IsPublic, c.CreatedAt, PhotoIds = c.Photos.Select(x => x.PhotoId).ToList() })
                .ToListAsync(ct),
            Portfolio = await db.Portfolios.AsNoTracking().Where(p => p.UserId == me)
                .Select(p => new
                {
                    p.Slug, p.IsPublic, p.Bio, p.Website, p.Instagram, p.ContactEmail,
                    PhotoIds = p.Photos.OrderBy(x => x.Position).Select(x => x.PhotoId).ToList(),
                }).SingleOrDefaultAsync(ct),
            Following = await db.Follows.AsNoTracking().Where(f => f.FollowerId == me).OrderBy(f => f.CreatedAt)
                .Select(f => new { Member = f.Followee.DisplayName, Since = f.CreatedAt }).ToListAsync(ct),
            Followers = await db.Follows.AsNoTracking().Where(f => f.FolloweeId == me).OrderBy(f => f.CreatedAt)
                .Select(f => new { Member = f.Follower.DisplayName, Since = f.CreatedAt }).ToListAsync(ct),
            BlockedMembers = await db.Blocks.AsNoTracking().Where(b => b.BlockerId == me).OrderBy(b => b.CreatedAt)
                .Select(b => new { Member = b.Blocked.DisplayName, Since = b.CreatedAt }).ToListAsync(ct),
            EmailNotifications = await db.EmailSubscriptions.AsNoTracking().Where(s => s.UserId == me)
                .Select(s => new { s.Email, s.Frequency, s.Enabled, Confirmed = s.ConfirmedAt != null, s.WeeklySummary, s.CreatedAt })
                .SingleOrDefaultAsync(ct),
            Reports = await db.Reports.AsNoTracking().Where(r => r.ReporterId == me).OrderBy(r => r.CreatedAt)
                .Select(r => new { r.Reason, r.Note, r.Status, r.CreatedAt }).ToListAsync(ct),
        };

        logger.LogInformation("User {UserId} downloaded their data ({PhotoCount} photos)", me, photos.Count);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, Json);
        return File(bytes, "application/json", $"photocommunity-my-data-{clock.GetUtcNow():yyyyMMdd}.json");
    }
}
