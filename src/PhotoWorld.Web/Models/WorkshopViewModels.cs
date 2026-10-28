using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Workshops;

namespace PhotoWorld.Web.Models;

public sealed record WorkshopsPageViewModel(string List, IReadOnlyList<WorkshopSummaryDto> Workshops);

/// <summary>A workshop's page: the workshop and, for those who can read it, the start of its chat.</summary>
public sealed record WorkshopPageViewModel(WorkshopDto Workshop, IReadOnlyList<WorkshopMessageDto> Messages);

/// <summary>
/// The workshop form. The browser fills in <see cref="StartsLocal"/> in the member's own time and <see cref="UtcOffsetMinutes"/>
/// (workshops.js), so the server can store the real moment without knowing their time zone.
/// </summary>
public sealed class WorkshopFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the workshop a title.")]
    [StringLength(WorkshopLimits.TitleMax)]
    public string Title { get; set; } = "";

    [StringLength(WorkshopLimits.DescriptionMax)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose when it starts.")]
    public DateTime? StartsLocal { get; set; }

    /// <summary>Minutes to add to UTC for the member's time zone (UK summer: 60).</summary>
    public int UtcOffsetMinutes { get; set; }

    [Range(30, 480, ErrorMessage = "Choose how long it lasts.")]
    public int DurationMinutes { get; set; } = 60;

    [StringLength(WorkshopLimits.VideoLinkMax)]
    [Url(ErrorMessage = "Use the full https:// link to your call.")]
    public string? VideoLink { get; set; }

    [Range(2, WorkshopLimits.MaxPeopleLimit, ErrorMessage = "Between 2 and 200 people, or leave it empty for no limit.")]
    public int? MaxPeople { get; set; }

    /// <summary>When editing: the stored start, which workshops.js turns into the member's local time.</summary>
    public DateTimeOffset? StartsAtUtc { get; set; }

    /// <summary>For a new workshop: public (null) or one of the member's clubs. Fixed once the workshop exists.</summary>
    public Guid? ClubId { get; set; }
    public string? ClubName { get; set; }
    public IReadOnlyList<PhotoWorld.Shared.Clubs.ClubSummaryDto> Clubs { get; set; } = [];

    public SaveWorkshopRequest ToRequest()
    {
        var start = new DateTimeOffset(DateTime.SpecifyKind(StartsLocal!.Value, DateTimeKind.Unspecified), TimeSpan.FromMinutes(UtcOffsetMinutes));
        return new SaveWorkshopRequest(Title, Description, start, start.AddMinutes(DurationMinutes), VideoLink, MaxPeople, ClubId);
    }

    public static WorkshopFormViewModel From(WorkshopDto w) => new()
    {
        Id = w.Workshop.Id, Title = w.Workshop.Title, Description = w.Description, VideoLink = w.VideoLink,
        MaxPeople = w.Workshop.MaxPeople, StartsAtUtc = w.Workshop.StartsAt,
        DurationMinutes = (int)(w.Workshop.EndsAt - w.Workshop.StartsAt).TotalMinutes,
        ClubId = w.Workshop.ClubId, ClubName = w.Workshop.ClubName,
    };
}
