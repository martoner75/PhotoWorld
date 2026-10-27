using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;
using PhotoWorld.Shared.Portfolios;

namespace PhotoWorld.App.ViewModels;

/// <summary>One of the member's photos on the portfolio page: chosen ones show their place in the order.</summary>
public partial class PortfolioPick(PhotoSummaryDto photo) : ObservableObject
{
    public PhotoSummaryDto Photo { get; } = photo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChosen), nameof(Badge))]
    public partial int? Position { get; set; }

    public bool IsChosen => Position is not null;
    public string Badge => Position is { } n ? n.ToString() : "";
}

/// <summary>
/// The member's public portfolio (the website's /p/{address}): switch it on, describe yourself, choose and order up to
/// 24 photos (tap in the order to show them), then share the link.
/// </summary>
public partial class PortfolioViewModel(PhotoWorldApi api) : ViewModelBase
{
    private readonly List<PortfolioPick> _order = [];
    private bool _loaded;

    public ObservableCollection<PortfolioPick> Picks { get; } = [];

    [ObservableProperty] public partial bool IsPublic { get; set; }
    [ObservableProperty] public partial string Slug { get; set; } = "";
    [ObservableProperty] public partial string Bio { get; set; } = "";
    [ObservableProperty] public partial string Website { get; set; } = "";
    [ObservableProperty] public partial string Instagram { get; set; } = "";
    [ObservableProperty] public partial string ContactEmail { get; set; } = "";

    /// <summary>The saved state (sharing a link only makes sense once it's public and saved).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Link), nameof(CanShare))]
    public partial MyPortfolioDto? Saved { get; set; }

    [ObservableProperty] public partial string ChosenSummary { get; set; } = "";

    public Uri? Link => Saved is { IsPublic: true } s ? new Uri(AppConfig.WebBaseUrl, $"p/{s.Slug}") : null;
    public bool CanShare => Link is not null;
    public bool HasCandidates => Picks.Count > 0;

    public int SlugMaxLength => PortfolioLimits.SlugMax;
    public int BioMaxLength => PortfolioLimits.BioMax;
    public int LinkMaxLength => PortfolioLimits.LinkMax;

    public Task LoadAsync()
    {
        if (_loaded) return Task.CompletedTask;
        return RunAsync(async () =>
        {
            Apply(await api.GetMyPortfolioAsync());
            _loaded = true;
        });
    }

    private void Apply(MyPortfolioDto dto)
    {
        Saved = dto;
        (IsPublic, Slug, Bio, Website) = (dto.IsPublic, dto.Slug, dto.Bio ?? "", dto.Website ?? "");
        (Instagram, ContactEmail) = (dto.Instagram is null ? "" : "@" + dto.Instagram, dto.ContactEmail ?? "");

        Picks.Clear();
        _order.Clear();
        foreach (var photo in dto.Candidates) Picks.Add(new PortfolioPick(photo));
        foreach (var id in dto.PhotoIds)
            if (Picks.FirstOrDefault(p => p.Photo.Id == id) is { } pick) _order.Add(pick);
        Renumber();
        OnPropertyChanged(nameof(HasCandidates));
    }

    /// <summary>Tapping a photo adds it at the end of the order, or takes it out.</summary>
    [RelayCommand]
    private async Task TogglePickAsync(PortfolioPick pick)
    {
        if (pick.IsChosen) _order.Remove(pick);
        else if (_order.Count >= PortfolioLimits.PhotosMax)
        {
            await Ui.AlertAsync($"A portfolio can show up to {PortfolioLimits.PhotosMax} photos. Take one out first.");
            return;
        }
        else _order.Add(pick);
        Renumber();
    }

    private void Renumber()
    {
        foreach (var pick in Picks) pick.Position = null;
        for (var i = 0; i < _order.Count; i++) _order[i].Position = i + 1;
        ChosenSummary = _order.Count == 0 ? "No photos chosen yet." : $"{_order.Count} of up to {PortfolioLimits.PhotosMax} chosen, in this order.";
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var saved = await api.SavePortfolioAsync(new SavePortfolioRequest(IsPublic, Slug.Trim(), Blank(Bio), Blank(Website),
            Blank(Instagram), Blank(ContactEmail), _order.Select(p => p.Photo.Id).ToList()));
        Apply(saved);
        await Ui.AlertAsync(saved.IsPublic
            ? "Portfolio saved. It's public: tap Share link to send it to anyone."
            : "Portfolio saved. It's switched off, so nobody else can see it.");
    });

    [RelayCommand]
    private Task ShareAsync() => Link is not { } link ? Task.CompletedTask
        : Share.Default.RequestAsync(new ShareTextRequest { Uri = link.ToString(), Title = "My PhotoCommunity portfolio" });

    [RelayCommand]
    private Task OpenAsync() => Link is not { } link ? Task.CompletedTask : Ui.OpenInBrowserAsync(link);

    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
