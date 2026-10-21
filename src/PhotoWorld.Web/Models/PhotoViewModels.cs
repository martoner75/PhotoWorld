using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Web.Models;

public sealed record PhotoPageViewModel(PhotoDetailDto Photo, IReadOnlyList<CommentDto> Comments, PhotoFeedbackDto? Feedback = null);

public sealed record MemberPageViewModel(MemberProfileDto Member, PagedResult<PhotoSummaryDto> Photos);

/// <summary>The gallery: everyone's photos or only followed members', optionally filtered by camera settings.</summary>
public sealed record GalleryViewModel(PagedResult<PhotoSummaryDto> Photos, bool Following, PhotoSearch Search, CameraOptionsDto Options,
    PhotoOfTheDayDto? PhotoOfTheDay = null);

public sealed record ExplorePageViewModel(ExploreDto Explore, PhotoOfTheDayDto? PhotoOfTheDay);

public sealed class UploadPhotoViewModel
{
    [Required(ErrorMessage = "Choose a photo to upload.")]
    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "Give your photo a title.")]
    [StringLength(PhotoUploadLimits.MaxTitleLength)]
    public string Title { get; set; } = "";

    [StringLength(PhotoUploadLimits.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool ShowOnHome { get; set; }

    /// <summary>One of the member's places, to show the photo on the Spots map. Optional.</summary>
    public Guid? PlaceId { get; set; }

    /// <summary>Tags as typed ("fog wildlife london"); #hashtags in the description are added too.</summary>
    [StringLength(600)]
    public string? Tags { get; set; }

    public bool WantsFeedback { get; set; }

    /// <summary>The member's places for the picker (filled in by the controller).</summary>
    public IReadOnlyList<PhotoWorld.Shared.Places.PlaceDto> Places { get; set; } = [];
}

public sealed class EditPhotoViewModel
{
    public Guid Id { get; set; }
    public string? ThumbnailUrl { get; set; }

    [Required(ErrorMessage = "Give your photo a title.")]
    [StringLength(PhotoUploadLimits.MaxTitleLength)]
    public string Title { get; set; } = "";

    [StringLength(PhotoUploadLimits.MaxDescriptionLength)]
    public string? Description { get; set; }

    public bool ShowOnHome { get; set; }

    public Guid? PlaceId { get; set; }
    public IReadOnlyList<PhotoWorld.Shared.Places.PlaceDto> Places { get; set; } = [];

    [StringLength(600)]
    public string? Tags { get; set; }

    public bool WantsFeedback { get; set; }
}

/// <summary>The Spots map; <see cref="Focus"/> is a photo to open on arrival.</summary>
public sealed record SpotsPageViewModel(IReadOnlyList<PhotoSpotDto> Spots, Guid? Focus);

/// <summary>One of the member's own places for a photo's "where was it taken" picker.</summary>
public sealed record PlaceFieldViewModel(Guid? Selected, IReadOnlyList<PhotoWorld.Shared.Places.PlaceDto> Places);

/// <summary>One comment plus the photo it belongs to (needed for the delete form).</summary>
public sealed record CommentItemViewModel(Guid PhotoId, CommentDto Comment);

public sealed record PagerViewModel(int Page, int TotalPages);

/// <summary>"Everyone / Following" tabs on the current page (links keep the page's other query values).</summary>
public sealed record FeedTabsViewModel(bool Following);

public static class CameraDetailsDisplay
{
    /// <summary>
    /// Label/value rows for the camera panel, skipping anything the photo didn't record. <c>Search</c> is the
    /// gallery filter for "more photos like this" (query-string values), where there is one.
    /// </summary>
    public static IEnumerable<(string Label, string Value, Dictionary<string, string>? Search)> Rows(this CameraDetailsDto c)
    {
        var camera = c.Model is not null && c.Make is not null && !c.Model.StartsWith(c.Make, StringComparison.OrdinalIgnoreCase)
            ? $"{c.Make} {c.Model}"
            : c.Model ?? c.Make;
        if (camera is not null) yield return ("Camera", camera, Filter(new PhotoSearch { Camera = c.Model ?? c.Make }));
        if (c.Lens is not null) yield return ("Lens", c.Lens, Filter(new PhotoSearch { Lens = c.Lens }));
        if (c.FocalLengthMm is { } focal) yield return ("Focal length", $"{focal:0.#} mm", Filter(new PhotoSearch { FocalMin = focal, FocalMax = focal }));
        if (c.Aperture is { } aperture) yield return ("Aperture", $"ƒ/{aperture:0.#}", Filter(new PhotoSearch { ApertureMin = aperture, ApertureMax = aperture }));
        if (c.ExposureTime is not null) yield return ("Shutter", c.ExposureTime, null);
        if (c.Iso is { } iso) yield return ("ISO", iso.ToString(), Filter(new PhotoSearch { IsoMin = iso, IsoMax = iso }));
        if (c.TakenAt is { } taken) yield return ("Taken", taken.ToString("d MMM yyyy, HH:mm"), null);
    }

    private static Dictionary<string, string> Filter(PhotoSearch search) =>
        search.QueryValues().ToDictionary(v => v.Name, v => v.Value);
}

public sealed class ReportViewModel
{
    /// <summary>The photo or blog post; with <see cref="CommentId"/>, the comment on it being reported.</summary>
    public Guid Id { get; set; }
    public Guid? CommentId { get; set; }

    // What's being reported, shown at the top of the form (filled in by the controller).
    public string Subject { get; set; } = "photo";
    public string? Title { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? QuotedText { get; set; }
    public string? CancelUrl { get; set; }

    [Required(ErrorMessage = "Choose a reason.")]
    public string? Reason { get; set; }

    [StringLength(ReportRules.MaxNoteLength)]
    public string? Note { get; set; }
}

/// <summary>Banner shown to the owner of content that moderation is hiding from everyone else.</summary>
public sealed record ModerationBannerModel(ModerationState State, string Noun);
