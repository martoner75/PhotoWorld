using System.ComponentModel.DataAnnotations;
using System.Globalization;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Clubs;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Web.Models;

/// <summary>The club directory (search, optionally near one of the member's places) or the member's own clubs.</summary>
public sealed record ClubsPageViewModel(
    bool Mine,
    string? Query,
    Guid? NearPlace,
    IReadOnlyList<PlaceDto> Places,
    PagedResult<ClubSummaryDto> Clubs);

/// <summary>A club's page: its feed of shared photos, its members, and the requests waiting when the viewer runs the club.</summary>
public sealed record ClubPageViewModel(ClubDto Club, IReadOnlyList<ClubMemberDto> Members, IReadOnlyList<ClubMemberDto> Requests,
    PagedResult<PhotoSummaryDto> Photos,
    IReadOnlyList<PhotoWorld.Shared.Walks.WalkSummaryDto> Walks,
    IReadOnlyList<PhotoWorld.Shared.Challenges.ChallengeSummaryDto> Challenges,
    IReadOnlyList<PhotoWorld.Shared.Workshops.WorkshopSummaryDto> Workshops);

/// <summary>"Share to clubs" for one of the member's photos: the approved clubs they're in.</summary>
public sealed record ShareToClubsViewModel(PhotoDetailDto Photo, IReadOnlyList<ClubShareChoiceDto> Choices);

public sealed class ClubFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the club a name.")]
    [StringLength(ClubLimits.NameMax)]
    public string Name { get; set; } = "";

    [StringLength(ClubLimits.CityMax)]
    public string? City { get; set; }

    [StringLength(ClubLimits.DescriptionMax)]
    public string? Description { get; set; }

    public string JoinPolicy { get; set; } = ClubJoinPolicy.Open;

    /// <summary>"51.44,-0.27" from one of the member's places, to sort the directory by distance; empty for none.</summary>
    public string? Area { get; set; }

    public IReadOnlyList<PlaceDto> Places { get; set; } = [];

    public static ClubFormViewModel From(ClubDto club) => new()
    {
        Id = club.Summary.Id,
        Name = club.Summary.Name,
        City = club.Summary.City,
        Description = club.Description,
        JoinPolicy = club.Summary.JoinPolicy,
        Area = club.Latitude is { } lat && club.Longitude is { } lon ? AreaValue(lat, lon) : null,
    };

    public static string AreaValue(double latitude, double longitude) =>
        $"{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}";

    public SaveClubRequest ToRequest()
    {
        double? lat = null, lon = null;
        var parts = (Area ?? "").Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            (lat, lon) = (a, b);
        return new SaveClubRequest(Name, Description, City, JoinPolicy, lat, lon);
    }
}

/// <summary>A site admin's Approve / Reject (or Take down) for a club; <see cref="ReturnUrl"/> is where they go back to.</summary>
public sealed record ClubReviewActionsModel(Guid ClubId, string Name, string ReviewStatus, string ReturnUrl);
