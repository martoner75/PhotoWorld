using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Activities;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

/// <summary>One notification, with its wording worked out once.</summary>
public sealed record ActivityItem(ActivityDto Activity)
{
    public string Text => ActivityText.Describe(Activity);
    public string? Snippet => Activity.Snippet is null ? null : $"“{Activity.Snippet}”";
    public string When => ActivityText.Ago(Activity.CreatedAt, DateTimeOffset.UtcNow);
    public string? AvatarUrl => Activity.Actor?.AvatarUrl;
    public string Initial => Activity.Actor is { DisplayName.Length: > 0 } a ? a.DisplayName[..1] : "🏆";
    public bool IsUnread => !Activity.IsRead;
}

public partial class ActivityViewModel(PhotoWorldApi api) : PagedListViewModel<ActivityItem>
{
    private bool _markedRead;

    protected override async Task<PagedResult<ActivityItem>> FetchAsync(int page)
    {
        var activity = await api.GetActivityAsync(page);
        // Opening the screen reads everything; the new ones stay highlighted until the next visit.
        if (page == 1 && !_markedRead && activity.Items.Any(a => !a.IsRead))
        {
            _markedRead = true;
            try { await api.MarkActivityReadAsync(); } catch (ApiException) { /* try again next time */ }
        }
        return new PagedResult<ActivityItem>(activity.Items.Select(a => new ActivityItem(a)).ToList(),
            activity.Page, activity.PageSize, activity.TotalCount);
    }

    /// <summary>Opens what the notification is about: the photo, post, challenge or member.</summary>
    [RelayCommand]
    private Task OpenAsync(ActivityItem item) => item.Activity switch
    {
        { PhotoId: { } photo } => Shell.Current.GoToAsync(Routes.Photo, new ShellNavigationQueryParameters { ["id"] = photo }),
        { PostId: { } post } => Shell.Current.GoToAsync(Routes.Post, new ShellNavigationQueryParameters { ["id"] = post }),
        { ChallengeId: { } challenge } => Routes.OpenChallengeAsync(challenge),
        { WalkId: { } walk } => Routes.OpenWalkAsync(walk),
        { Actor: { } actor } => Routes.OpenMemberAsync(actor.Id),
        _ => Task.CompletedTask,
    };
}
