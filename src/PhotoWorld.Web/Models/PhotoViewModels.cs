using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Web.Models;

public sealed record PhotoPageViewModel(PhotoDetailDto Photo, IReadOnlyList<CommentDto> Comments);

public sealed record MemberPageViewModel(MemberProfileDto Member, PagedResult<PhotoSummaryDto> Photos);

public sealed class UploadPhotoViewModel
{
    [Required(ErrorMessage = "Choose a photo to upload.")]
    public IFormFile? Photo { get; set; }

    [Required(ErrorMessage = "Give your photo a title.")]
    [StringLength(PhotoUploadLimits.MaxTitleLength)]
    public string Title { get; set; } = "";

    [StringLength(PhotoUploadLimits.MaxDescriptionLength)]
    public string? Description { get; set; }
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
}

/// <summary>One comment plus the photo it belongs to (needed for the delete form).</summary>
public sealed record CommentItemViewModel(Guid PhotoId, CommentDto Comment);

public sealed record PagerViewModel(int Page, int TotalPages);

public static class CameraDetailsDisplay
{
    /// <summary>Label/value rows for the camera panel, skipping anything the photo didn't record.</summary>
    public static IEnumerable<(string Label, string Value)> Rows(this CameraDetailsDto c)
    {
        var camera = c.Model is not null && c.Make is not null && !c.Model.StartsWith(c.Make, StringComparison.OrdinalIgnoreCase)
            ? $"{c.Make} {c.Model}"
            : c.Model ?? c.Make;
        if (camera is not null) yield return ("Camera", camera);
        if (c.Lens is not null) yield return ("Lens", c.Lens);
        if (c.FocalLengthMm is { } focal) yield return ("Focal length", $"{focal:0.#} mm");
        if (c.Aperture is { } aperture) yield return ("Aperture", $"ƒ/{aperture:0.#}");
        if (c.ExposureTime is not null) yield return ("Shutter", c.ExposureTime);
        if (c.Iso is { } iso) yield return ("ISO", iso.ToString());
        if (c.TakenAt is { } taken) yield return ("Taken", taken.ToString("d MMM yyyy, HH:mm"));
    }
}
