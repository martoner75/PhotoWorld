using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Collections;

public static class CollectionLimits
{
    public const int TitleMax = 80;
    public const int DescriptionMax = 500;
    public const int MaxPerMember = 100;
    public const int MaxPhotos = 1000;
}

/// <summary>
/// A member's collection of photos (theirs or anyone's). Private ones are only ever shown to their owner; public ones
/// appear on the owner's member page. <see cref="PhotoCount"/> counts the photos the viewer can see.
/// </summary>
public sealed record CollectionSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublic,
    UserSummaryDto Owner,
    int PhotoCount,
    string? CoverUrl,
    DateTimeOffset UpdatedAt,
    bool IsMine);

public sealed record CollectionDto(CollectionSummaryDto Collection, PagedResult<PhotoSummaryDto> Photos);

public sealed record SaveCollectionRequest(
    [Required, StringLength(CollectionLimits.TitleMax, MinimumLength = 1)] string Title,
    [StringLength(CollectionLimits.DescriptionMax)] string? Description,
    bool IsPublic);

/// <summary>One of the viewer's collections, and whether a given photo is in it (for the "Save" choice).</summary>
public sealed record CollectionChoiceDto(Guid Id, string Title, bool IsPublic, bool ContainsPhoto);
