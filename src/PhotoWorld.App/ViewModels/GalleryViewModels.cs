using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

public partial class GalleryViewModel : PagedListViewModel<PhotoSummaryDto>
{
    private readonly PhotoWorldApi _api;

    public GalleryViewModel(PhotoWorldApi api)
    {
        _api = api;
        // A new upload belongs at the top: start over next time the gallery shows.
        WeakReferenceMessenger.Default.Register<GalleryViewModel, PhotoUploadedMessage>(this, (vm, _) => vm.Items.Clear());
    }

    protected override Task<PagedResult<PhotoSummaryDto>> FetchAsync(int page) => _api.GetPhotosAsync(page);

    [RelayCommand]
    private Task OpenAsync(PhotoSummaryDto photo) =>
        Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo.Id });
}

public partial class PhotoDetailViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Byline), nameof(CameraSummary), nameof(IsUnderReview), nameof(AspectHeight))]
    public partial PhotoDetailDto? Photo { get; set; }

    [ObservableProperty]
    public partial bool LikedByMe { get; set; }

    [ObservableProperty]
    public partial int LikeCount { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommentCommand))]
    public partial string NewComment { get; set; } = "";

    public ObservableCollection<CommentDto> Comments { get; } = [];

    public int CommentMaxLength => PhotoUploadLimits.MaxCommentLength;

    public string Byline => Photo is null ? "" : $"by {Photo.Owner.DisplayName} · {Photo.CreatedAt.LocalDateTime:d MMM yyyy}";

    public bool IsUnderReview => Photo is { Moderation: not ModerationState.Visible };

    /// <summary>"Canon EOS R6 · 35 mm · f/1.8 · 1/250s · ISO 400", from whatever the photo recorded.</summary>
    public string? CameraSummary
    {
        get
        {
            if (Photo?.Camera is not { IsEmpty: false } c) return null;
            var camera = string.Join(' ', new[] { c.Make, c.Model }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
            string?[] parts =
            [
                camera, c.Lens,
                c.FocalLengthMm is { } f ? $"{f:0.#} mm" : null,
                c.Aperture is { } a ? $"f/{a:0.#}" : null,
                c.ExposureTime,
                c.Iso is { } iso ? $"ISO {iso}" : null,
                c.TakenAt is { } t ? $"taken {t.LocalDateTime:d MMM yyyy}" : null,
            ];
            return string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    /// <summary>Height that shows the whole photo at the screen's width (capped so tall shots don't fill the page).</summary>
    public double AspectHeight => Photo is { Width: > 0 } p
        ? Math.Min(DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density * p.Height / p.Width, 640)
        : 300;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid photoId) _ = LoadAsync(photoId);
    }

    private Task LoadAsync(Guid id) => RunAsync(async () =>
    {
        var photo = api.GetPhotoAsync(id);
        var comments = api.GetCommentsAsync(id);
        Photo = await photo;
        (LikedByMe, LikeCount) = (Photo.LikedByMe, Photo.LikeCount);
        Comments.Clear();
        foreach (var comment in await comments) Comments.Add(comment);
    });

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (Photo is null) return;
        // Optimistic: flip now, settle with the server's count.
        var like = !LikedByMe;
        (LikedByMe, LikeCount) = (like, LikeCount + (like ? 1 : -1));
        try
        {
            var state = await api.SetLikeAsync(Photo.Id, like);
            (LikedByMe, LikeCount) = (state.LikedByMe, state.LikeCount);
        }
        catch (ApiException ex)
        {
            (LikedByMe, LikeCount) = (!like, LikeCount - (like ? 1 : -1));
            await Ui.ShowAsync(ex);
        }
    }

    private bool CanAddComment() => !string.IsNullOrWhiteSpace(NewComment);

    [RelayCommand(CanExecute = nameof(CanAddComment))]
    private Task AddCommentAsync() => RunAsync(async () =>
    {
        Comments.Add(await api.AddCommentAsync(Photo!.Id, NewComment.Trim()));
        NewComment = "";
    });

    [RelayCommand]
    private Task DeleteCommentAsync(CommentDto comment) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Delete comment", "Delete this comment?", "Delete")) return;
        await api.DeleteCommentAsync(Photo!.Id, comment.Id);
        Comments.Remove(comment);
    });

    [RelayCommand]
    private Task ReportPhotoAsync() => ReportAsync(r => api.ReportPhotoAsync(Photo!.Id, null, r));

    [RelayCommand]
    private Task ReportCommentAsync(CommentDto comment) => ReportAsync(r => api.ReportPhotoAsync(Photo!.Id, comment.Id, r));
}
