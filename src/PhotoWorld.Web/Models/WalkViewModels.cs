using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Places;
using PhotoWorld.Shared.Walks;

namespace PhotoWorld.Web.Models;

public sealed record WalksPageViewModel(string List, IReadOnlyList<WalkSummaryDto> Walks);

/// <summary>
/// The walk form. The browser fills in <see cref="StartsLocal"/> in the member's own time and <see cref="UtcOffsetMinutes"/>
/// (walks.js), so the server can store the real moment without knowing their time zone.
/// </summary>
public sealed class WalkFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the walk a title.")]
    [StringLength(WalkLimits.TitleMax)]
    public string Title { get; set; } = "";

    [StringLength(WalkLimits.DescriptionMax)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose when it starts.")]
    public DateTime? StartsLocal { get; set; }

    /// <summary>Minutes to add to UTC for the member's time zone (UK summer: 60).</summary>
    public int UtcOffsetMinutes { get; set; }

    [Range(30, 720, ErrorMessage = "Choose how long it lasts.")]
    public int DurationMinutes { get; set; } = 120;

    [Required(ErrorMessage = "Say where to meet.")]
    [StringLength(WalkLimits.MeetingPointMax)]
    public string MeetingPoint { get; set; } = "";

    [Required(ErrorMessage = "Click the map to set the meeting point.")]
    [Range(-90.0, 90.0)]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Click the map to set the meeting point.")]
    [Range(-180.0, 180.0)]
    public double? Longitude { get; set; }

    [Range(2, WalkLimits.MaxPeopleLimit, ErrorMessage = "Between 2 and 200 people, or leave it empty for no limit.")]
    public int? MaxPeople { get; set; }

    /// <summary>When editing: the stored start, which walks.js turns into the member's local time.</summary>
    public DateTimeOffset? StartsAtUtc { get; set; }

    public IReadOnlyList<PlaceDto> Places { get; set; } = [];

    public SaveWalkRequest ToRequest()
    {
        var start = new DateTimeOffset(DateTime.SpecifyKind(StartsLocal!.Value, DateTimeKind.Unspecified), TimeSpan.FromMinutes(UtcOffsetMinutes));
        return new SaveWalkRequest(Title, Description, start, start.AddMinutes(DurationMinutes), MeetingPoint, Latitude!.Value,
            Longitude!.Value, MaxPeople);
    }

    public static WalkFormViewModel From(WalkDto w) => new()
    {
        Id = w.Walk.Id, Title = w.Walk.Title, Description = w.Description, MeetingPoint = w.Walk.MeetingPoint,
        Latitude = w.Walk.Latitude, Longitude = w.Walk.Longitude, MaxPeople = w.Walk.MaxPeople, StartsAtUtc = w.Walk.StartsAt,
        DurationMinutes = (int)(w.Walk.EndsAt - w.Walk.StartsAt).TotalMinutes,
    };
}
