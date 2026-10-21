using Microsoft.EntityFrameworkCore;
using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Photos;

public static class PhotoSummaries
{
    /// <summary>
    /// Gallery tiles for already-filtered, ordered and paged <paramref name="photos"/>, with counts as
    /// <paramref name="me"/> sees them (comments hidden by moderation aren't counted).
    /// </summary>
    public static async Task<List<PhotoSummaryDto>> ToSummariesAsync(this IQueryable<Photo> photos, Guid? me,
        SupabaseStorage storage, CancellationToken ct)
    {
        var commentVisible = Data.Moderation.CommentVisibleTo(me);
        var rows = await photos
            .Select(p => new
            {
                p.Id, p.Title, p.ThumbnailPath, p.Width, p.Height, p.CreatedAt,
                Owner = new UserSummaryDto(p.Owner.Id, p.Owner.DisplayName, p.Owner.AvatarUrl),
                Likes = p.Likes.Count,
                Comments = p.Comments.AsQueryable().Count(commentVisible),
                Upheld = p.Reports.Any(r => r.Status == ReportStatus.Upheld),
                OpenReports = p.Reports.Count(r => r.Status == ReportStatus.Open),
            })
            .ToListAsync(ct);

        return rows.Select(r => new PhotoSummaryDto(r.Id, r.Title, storage.PublicUrl(r.ThumbnailPath),
            r.Width, r.Height, r.Owner, r.Likes, r.Comments, r.CreatedAt,
            Data.Moderation.State(r.Upheld, r.OpenReports))).ToList();
    }
}
