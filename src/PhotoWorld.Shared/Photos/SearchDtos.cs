using PhotoWorld.Shared.Blogs;

namespace PhotoWorld.Shared.Photos;

public sealed record TagCountDto(string Name, int Count);

/// <summary>A member in search results or suggestions, with how much they've shared.</summary>
public sealed record MemberResultDto(UserSummaryDto Member, int PhotoCount, int FollowerCount, bool FollowedByMe);

/// <summary>What a search finds (each list is the best few; photos link to the full gallery search).</summary>
public sealed record SearchResultsDto(
    string Query,
    IReadOnlyList<MemberResultDto> Members,
    IReadOnlyList<TagCountDto> Tags,
    IReadOnlyList<PhotoSummaryDto> Photos,
    int PhotoCount,
    IReadOnlyList<BlogPostSummaryDto> Posts);

/// <summary>The Explore page: what's popular lately, beyond the members the viewer already follows.</summary>
public sealed record ExploreDto(
    IReadOnlyList<TagCountDto> TrendingTags,
    IReadOnlyList<PhotoSummaryDto> PopularPhotos,
    IReadOnlyList<MemberResultDto> SuggestedMembers);
