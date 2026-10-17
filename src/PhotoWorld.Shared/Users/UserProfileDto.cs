namespace PhotoWorld.Shared.Users;

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    string? Email,
    string? AvatarUrl,
    IReadOnlyList<string> LinkedProviders,
    DateTimeOffset MemberSince,
    DateTimeOffset LastLoginAt);
