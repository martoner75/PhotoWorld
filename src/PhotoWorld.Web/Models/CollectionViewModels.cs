using System.ComponentModel.DataAnnotations;
using PhotoWorld.Shared.Collections;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Web.Models;

/// <summary>A list of collections: the viewer's own (<see cref="Owner"/> null) or a member's public ones.</summary>
public sealed record CollectionsPageViewModel(IReadOnlyList<CollectionSummaryDto> Collections, MemberProfileDto? Owner);

/// <summary>The "Save to a collection" page for one photo.</summary>
public sealed record SavePhotoViewModel(PhotoDetailDto Photo, IReadOnlyList<CollectionChoiceDto> Choices);

public sealed class CollectionFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Give the collection a name.")]
    [StringLength(CollectionLimits.TitleMax)]
    [Display(Name = "Name")]
    public string Title { get; set; } = "";

    [StringLength(CollectionLimits.DescriptionMax)]
    public string? Description { get; set; }

    [Display(Name = "Public: show it on my member page")]
    public bool IsPublic { get; set; }

    public SaveCollectionRequest ToRequest() => new(Title, Description, IsPublic);
}
