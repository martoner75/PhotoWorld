using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

public sealed record FeedbackItem(FeedbackDto Feedback)
{
    public string Ratings => $"Composition {Feedback.Composition} · Light {Feedback.Light} · Focus {Feedback.Focus}";
}

/// <summary>
/// A photo's feedback: the owner sees averages and everyone's feedback (and can remove it); other members give or
/// change their own. Private between the giver and the owner.
/// </summary>
public partial class FeedbackViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _photoId;

    public IReadOnlyList<string> Scale { get; } = ["1", "2", "3", "4", "5"];
    public ObservableCollection<FeedbackItem> Items { get; } = [];

    [ObservableProperty] public partial string Title { get; set; } = "Feedback";
    [ObservableProperty] public partial bool IsOwner { get; set; }
    [ObservableProperty] public partial bool CanGive { get; set; }
    [ObservableProperty] public partial bool HasMine { get; set; }
    [ObservableProperty] public partial string Summary { get; set; } = "";
    [ObservableProperty] public partial int Composition { get; set; } = -1;
    [ObservableProperty] public partial int Light { get; set; } = -1;
    [ObservableProperty] public partial int Focus { get; set; } = -1;
    [ObservableProperty] public partial string Comment { get; set; } = "";

    public int CommentMax => FeedbackLimits.CommentMax;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid photoId) _photoId = photoId;
        if (query.TryGetValue("title", out var title) && title is string t) Title = $"Feedback · {t}";
        if (query.TryGetValue("owner", out var owner) && owner is bool isOwner) IsOwner = isOwner;
        _ = LoadAsync();
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var f = await api.GetFeedbackAsync(_photoId);
        Items.Clear();
        foreach (var item in f.Items) Items.Add(new FeedbackItem(item));
        CanGive = f.CanGive;
        HasMine = f.Mine is not null;
        if (f.Mine is { } mine)
            (Composition, Light, Focus, Comment) = (mine.Composition - 1, mine.Light - 1, mine.Focus - 1, mine.Comment ?? "");
        Summary = f switch
        {
            { Averages: { } a } => $"{f.Count} {(f.Count == 1 ? "member" : "members")} · composition {a.Composition:0.0} · light {a.Light:0.0} · focus {a.Focus:0.0} (out of 5). Only you see this.",
            { CanGive: true } => "The photographer is asking for feedback. Be kind and specific: only they will see it.",
            { WantsFeedback: true } when IsOwner => "No feedback yet. Only you will see it when it comes.",
            _ when IsOwner => "This photo isn't asking for feedback. On the website, edit the photo and tick “Ask for feedback”.",
            _ => "This photo isn't asking for feedback any more.",
        };
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (Composition < 0 || Light < 0 || Focus < 0)
        {
            await Ui.AlertAsync("Rate composition, light and focus first (1 to 5).");
            return;
        }
        await api.SaveFeedbackAsync(_photoId, new SaveFeedbackRequest(Composition + 1, Light + 1, Focus + 1, Comment.Trim()));
        await Ui.AlertAsync("Thanks! Your feedback is with the photographer.");
        await Shell.Current.GoToAsync("..");
    });

    [RelayCommand]
    private Task DeleteMineAsync() => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Delete feedback", "Delete your feedback on this photo?", "Delete")) return;
        await api.DeleteFeedbackAsync(_photoId, null);
        await Shell.Current.GoToAsync("..");
    });

    [RelayCommand]
    private Task RemoveAsync(FeedbackItem item) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Remove feedback", $"Remove {item.Feedback.Author.DisplayName}'s feedback?", "Remove")) return;
        await api.DeleteFeedbackAsync(_photoId, item.Feedback.Id);
        Items.Remove(item);
    });
}
