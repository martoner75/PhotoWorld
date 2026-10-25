using System.ComponentModel.DataAnnotations;
using System.Globalization;
using PhotoWorld.Shared.Places;

namespace PhotoWorld.Web.Models;

/// <summary>The map page: all the member's places, plus the add/edit form (open when it has errors).</summary>
public sealed record PlacesPageViewModel(IReadOnlyList<PlaceDto> Places, PlaceFormViewModel Form, bool FormOpen);

public sealed class PlaceFormViewModel
{
    /// <summary>Set when editing an existing place.</summary>
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the place a name.")]
    [StringLength(PlaceLimits.NameMax)]
    public string Name { get; set; } = "";

    [StringLength(PlaceLimits.NoteMax)]
    public string? Note { get; set; }

    // Filled in by the map (a click, a dragged pin or a search result). Strings, because form values bind in the
    // server's culture and the map always writes "51.5072"; see TryGetCoordinates.
    [Required(ErrorMessage = "Pick the place on the map.")]
    public string? Latitude { get; set; }

    [Required(ErrorMessage = "Pick the place on the map.")]
    public string? Longitude { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? VisitedOn { get; set; }

    /// <summary>Tell the member the evening before when the golden-hour light here looks good.</summary>
    public bool ForecastAlerts { get; set; }

    public bool TryGetCoordinates(out double latitude, out double longitude)
    {
        longitude = 0;
        return double.TryParse(Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
               && double.TryParse(Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
               && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    }
}
