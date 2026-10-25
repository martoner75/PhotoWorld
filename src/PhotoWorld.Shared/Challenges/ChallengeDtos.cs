using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Shared.Challenges;

public static class ChallengeLimits
{
    public const int TitleMax = 100;
    public const int DescriptionMax = 1000;

    /// <summary>Photos one member may enter in one challenge.</summary>
    public const int EntriesPerMember = 3;
}

/// <summary>Where a challenge is in its life, from its start and end times.</summary>
public enum ChallengePhase { Upcoming, Open, Closed }

/// <param name="CoverUrl">The winning photo once closed, otherwise the most-voted entry so far (null without entries).</param>
/// <param name="Winner">Set once the challenge is closed and had entries.</param>
public sealed record ChallengeSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    ChallengePhase Phase,
    int EntryCount,
    string? CoverUrl,
    UserSummaryDto? Winner,
    Guid? ClubId = null,
    string? ClubName = null);

/// <summary>
/// One entry: the photo, and its votes (likes from other members between the moment it was entered and the
/// challenge's end). <see cref="Rank"/> is 1 for the leader; ties go to the earlier entry.
/// </summary>
public sealed record ChallengeEntryDto(PhotoSummaryDto Photo, int Votes, int Rank, bool IsMine, DateTimeOffset EnteredAt);

public sealed record ChallengeDto(
    ChallengeSummaryDto Challenge,
    IReadOnlyList<ChallengeEntryDto> Entries,
    int MyEntryCount,
    bool CanManage = false,
    bool CanEnter = true);

public sealed record EnterChallengeRequest([Required] Guid PhotoId);

/// <summary>
/// Creates or changes a challenge: site-wide (site admins), or for a club (<paramref name="ClubId"/>, its owner and
/// admins; fixed once created). Times are absolute; the website enters them in UTC.
/// </summary>
public sealed record SaveChallengeRequest(
    [Required, StringLength(ChallengeLimits.TitleMax)] string Title,
    [StringLength(ChallengeLimits.DescriptionMax)] string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    Guid? ClubId = null);
