using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared;
using PhotoWorld.Shared.Collabs;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Web.Models;

/// <summary>The board's filters, as they appear in the address so a search can be shared or bookmarked.</summary>
public sealed record CollabFilters(string List, string? Role, string? Terms, string? Q, Guid? NearPlace, double Km)
{
    public static readonly double[] Distances = [10, 25, 50, 100, 150];

    /// <summary>The nearPlace value for "Anywhere" in links (the form sends it empty).</summary>
    public const string AnywhereValue = "anywhere";

    /// <summary>Route values for a link to the board with one filter changed.</summary>
    public IDictionary<string, string> With(string? role = null, string? terms = null, bool clearRole = false, bool clearTerms = false)
    {
        var values = new Dictionary<string, string>();
        if (List != CollabLists.Open) values["list"] = List;
        var r = clearRole ? null : role ?? Role;
        var t = clearTerms ? null : terms ?? Terms;
        if (r is not null) values["role"] = r;
        if (t is not null) values["terms"] = t;
        if (!string.IsNullOrWhiteSpace(Q)) values["q"] = Q;
        // "Anywhere" is a choice too: without it the board would go back to the member's first place.
        values["nearPlace"] = NearPlace?.ToString() ?? AnywhereValue;
        if (Km != CollabLimits.DefaultNearKm) values["km"] = Km.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return values;
    }
}

public sealed record CollabsPageViewModel(CollabFilters Filters, PagedResult<CollabSummaryDto> Collabs, IReadOnlyList<PlaceDto> Places);

/// <summary>A call's page: the call and, for those who can read it, the start of its chat.</summary>
public sealed record CollabPageViewModel(CollabDto Collab, IReadOnlyList<CollabMessageDto> Messages);

/// <summary>
/// The call form. The browser fills in <see cref="StartsLocal"/> in the member's own time and <see cref="UtcOffsetMinutes"/>
/// (workshops.js does it for any form with a start time), so the server stores the real moment.
/// </summary>
public sealed class CollabFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the call a title.")]
    [StringLength(CollabLimits.TitleMax)]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Say who you're looking for.")]
    public string Role { get; set; } = CollabRoles.Model;

    [Required(ErrorMessage = "Say what people get for taking part.")]
    public string Terms { get; set; } = CollabTerms.Trade;

    [StringLength(CollabLimits.TermsNoteMax)]
    public string? TermsNote { get; set; }

    [StringLength(CollabLimits.DescriptionMax)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose when it starts.")]
    public DateTime? StartsLocal { get; set; }

    public int UtcOffsetMinutes { get; set; }

    [Range(30, 720, ErrorMessage = "Choose how long it lasts.")]
    public int DurationMinutes { get; set; } = 90;

    [Required(ErrorMessage = "Say roughly where it is.")]
    [StringLength(CollabLimits.AreaMax)]
    public string Area { get; set; } = "";

    [Required(ErrorMessage = "Pick one of your places as the meeting point.")]
    public Guid? PlaceId { get; set; }

    public List<Guid> PhotoIds { get; set; } = [];

    /// <summary>When editing: the stored start, which workshops.js turns into the member's local time.</summary>
    public DateTimeOffset? StartsAtUtc { get; set; }
    /// <summary>When editing: the meeting point stored on the call, in case it's no longer one of the member's places.</summary>
    public string? CurrentMeetingPoint { get; set; }

    public IReadOnlyList<PlaceDto> Places { get; set; } = [];
    public IReadOnlyList<PhotoSummaryDto> Candidates { get; set; } = [];

    public SaveCollabRequest ToRequest()
    {
        var start = new DateTimeOffset(DateTime.SpecifyKind(StartsLocal!.Value, DateTimeKind.Unspecified), TimeSpan.FromMinutes(UtcOffsetMinutes));
        return new SaveCollabRequest(Title, Role, Terms, TermsNote, Description, start, start.AddMinutes(DurationMinutes), Area,
            PlaceId!.Value, PhotoIds);
    }

    public static CollabFormViewModel From(CollabDto c) => new()
    {
        Id = c.Collab.Id, Title = c.Collab.Title, Role = c.Collab.Role, Terms = c.Collab.Terms, TermsNote = c.TermsNote,
        Description = c.Description, Area = c.Collab.Area, StartsAtUtc = c.Collab.StartsAt,
        DurationMinutes = (int)(c.Collab.EndsAt - c.Collab.StartsAt).TotalMinutes,
        PhotoIds = c.Photos.Select(p => p.Id).ToList(), CurrentMeetingPoint = c.MeetingPoint?.Name,
    };
}

/// <summary>Approve / Reject (or Take down) for a call, on the admin queue and the call's page.</summary>
public sealed record CollabReviewActionsModel(Guid CollabId, string Title, string ReviewStatus, string ReturnUrl);
