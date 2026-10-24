using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Portfolios;

namespace PhotoWorld.Web.Models;

/// <summary>The portfolio settings form. Chosen photos are ticked, and shown in the order of their numbers.</summary>
public sealed class PortfolioEditViewModel
{
    [Display(Name = "Show my portfolio publicly")]
    public bool IsPublic { get; set; }

    [Required(ErrorMessage = "Choose an address.")]
    [StringLength(PortfolioLimits.SlugMax)]
    [Display(Name = "Address")]
    public string Slug { get; set; } = "";

    [StringLength(PortfolioLimits.BioMax)]
    [Display(Name = "About you")]
    public string? Bio { get; set; }

    [StringLength(PortfolioLimits.LinkMax)]
    [Display(Name = "Website")]
    public string? Website { get; set; }

    [StringLength(PortfolioLimits.LinkMax)]
    [Display(Name = "Instagram")]
    public string? Instagram { get; set; }

    [StringLength(PortfolioLimits.LinkMax), EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
    [Display(Name = "Contact email")]
    public string? ContactEmail { get; set; }

    /// <summary>The ticked photos.</summary>
    public List<Guid> PhotoIds { get; set; } = [];

    /// <summary>The number typed next to each photo (lower first).</summary>
    public Dictionary<Guid, int> Order { get; set; } = [];

    /// <summary>The owner's photos that may be shown (loaded from the API, never bound from the form).</summary>
    [BindNever]
    public IReadOnlyList<PhotoSummaryDto> Candidates { get; set; } = [];

    /// <summary>Ticked photos in the order chosen: by number, then as they were listed.</summary>
    public List<Guid> OrderedPhotoIds() => PhotoIds.Distinct()
        .Select((id, index) => (id, index))
        .OrderBy(x => Order.TryGetValue(x.id, out var n) ? n : int.MaxValue).ThenBy(x => x.index)
        .Select(x => x.id).ToList();

    public static PortfolioEditViewModel From(MyPortfolioDto dto) => new()
    {
        IsPublic = dto.IsPublic, Slug = dto.Slug, Bio = dto.Bio, Website = dto.Website,
        Instagram = dto.Instagram is null ? null : "@" + dto.Instagram, ContactEmail = dto.ContactEmail,
        PhotoIds = dto.PhotoIds.ToList(),
        Order = dto.PhotoIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i + 1),
        Candidates = dto.Candidates,
    };
}
