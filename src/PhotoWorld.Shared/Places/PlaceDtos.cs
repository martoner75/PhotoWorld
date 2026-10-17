using System.ComponentModel.DataAnnotations;

namespace PhotoWorld.Shared.Places;

public static class PlaceLimits
{
    public const int NameMax = 150;
    public const int NoteMax = 1000;

    /// <summary>Plenty for a lifetime of travel, and keeps the map page a single small request.</summary>
    public const int MaxPerMember = 2000;
}

/// <summary>A place the member has been. Places are private: only their owner ever sees them.</summary>
public sealed record PlaceDto(
    Guid Id,
    string Name,
    string? Note,
    double Latitude,
    double Longitude,
    DateOnly? VisitedOn,
    DateTimeOffset CreatedAt);

public sealed record SavePlaceRequest(
    [Required, StringLength(PlaceLimits.NameMax, MinimumLength = 1)] string Name,
    [StringLength(PlaceLimits.NoteMax)] string? Note,
    [Range(-90.0, 90.0)] double Latitude,
    [Range(-180.0, 180.0)] double Longitude,
    DateOnly? VisitedOn);
