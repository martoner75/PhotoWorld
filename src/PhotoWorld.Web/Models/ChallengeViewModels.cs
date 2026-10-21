using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Challenges;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Web.Models;

/// <summary>The "enter a photo" page: the member's own photos, marking those already in the challenge.</summary>
public sealed record EnterChallengeViewModel(ChallengeDto Challenge, PagedResult<PhotoSummaryDto> MyPhotos)
{
    public bool IsEntered(Guid photoId) => Challenge.Entries.Any(e => e.IsMine && e.Photo.Id == photoId);
    public int EntriesLeft => Math.Max(0, ChallengeLimits.EntriesPerMember - Challenge.MyEntryCount);
}

/// <summary>An entry tile; <see cref="CanWithdraw"/> while the challenge is open and the photo is the viewer's.</summary>
public sealed record ChallengeEntryItemViewModel(Guid ChallengeId, ChallengeEntryDto Entry, ChallengePhase Phase)
{
    public bool CanWithdraw => Entry.IsMine && Phase == ChallengePhase.Open;
}

public sealed record ChallengesIndexViewModel(IReadOnlyList<ChallengeSummaryDto> Challenges, bool IsAdmin);

/// <summary>The admin form for a challenge. Times are entered and shown in UTC (datetime-local has no time zone).</summary>
public sealed class ChallengeFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the challenge a theme.")]
    [StringLength(ChallengeLimits.TitleMax)]
    [Display(Name = "Theme")]
    public string Title { get; set; } = "";

    [StringLength(ChallengeLimits.DescriptionMax)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose when it starts.")]
    [Display(Name = "Starts (UTC)")]
    public DateTime? StartsAt { get; set; }

    [Required(ErrorMessage = "Choose when it ends.")]
    [Display(Name = "Ends (UTC)")]
    public DateTime? EndsAt { get; set; }

    /// <summary>Entries so far, shown as a warning when editing dates.</summary>
    public int EntryCount { get; set; }

    /// <summary>A new form: the coming calendar month, from midnight UTC on the 1st.</summary>
    public static ChallengeFormViewModel ForNext(DateTimeOffset now)
    {
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return new ChallengeFormViewModel { StartsAt = start, EndsAt = start.AddMonths(1) };
    }

    public static ChallengeFormViewModel From(ChallengeSummaryDto c) => new()
    {
        Id = c.Id, Title = c.Title, Description = c.Description,
        StartsAt = c.StartsAt.UtcDateTime, EndsAt = c.EndsAt.UtcDateTime, EntryCount = c.EntryCount,
    };

    public SaveChallengeRequest ToRequest() => new(Title, Description, Utc(StartsAt!.Value), Utc(EndsAt!.Value));

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.Zero);
}
