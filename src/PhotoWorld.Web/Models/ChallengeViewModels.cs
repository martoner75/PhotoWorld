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
