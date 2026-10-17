using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Blogs;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

public partial class BlogsViewModel(PhotoWorldApi api) : PagedListViewModel<BlogPostSummaryDto>
{
    protected override Task<PagedResult<BlogPostSummaryDto>> FetchAsync(int page) => api.GetPostsAsync(page);

    [RelayCommand]
    private Task OpenAsync(BlogPostSummaryDto post) =>
        Shell.Current.GoToAsync(Routes.Post, new ShellNavigationQueryParameters { ["id"] = post.Id });
}

public partial class PostViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommentsLabel))]
    public partial BlogPostDto? Post { get; set; }

    [ObservableProperty]
    public partial HtmlWebViewSource? Body { get; set; }

    public string CommentsLabel => Post is null ? "Comments" : $"Comments ({Post.CommentCount})";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid postId) _ = LoadAsync(postId);
    }

    private Task LoadAsync(Guid id) => RunAsync(async () =>
    {
        Post = await api.GetPostAsync(id);
        Body = new HtmlWebViewSource { Html = RenderPage(Post) };
    });

    [RelayCommand]
    private Task OpenCommentsAsync() => Post is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync(Routes.PostComments, new ShellNavigationQueryParameters { ["id"] = Post.Id, ["title"] = Post.Title });

    [RelayCommand]
    private Task ReportPostAsync() => ReportAsync(r => api.ReportPostAsync(Post!.Id, null, r));

    /// <summary>
    /// The post as a small dark page. The body is HTML the API has already sanitized (BlogHtml: basic formatting,
    /// safe links and the author's own photos); the title and name are encoded here.
    /// </summary>
    internal static string RenderPage(BlogPostDto post)
    {
        var date = (post.PublishedAt ?? post.UpdatedAt).LocalDateTime.ToString("d MMMM yyyy");
        var draft = post.Status == BlogPostStatus.Draft ? "<p class=\"draft\">Draft: only you can see this post.</p>" : "";
        return $$"""
            <!DOCTYPE html>
            <html><head>
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
              :root { color-scheme: dark; }
              body { background: #0e0f12; color: #ecebe7; font: 17px/1.6 -apple-system, system-ui, sans-serif; margin: 0; padding: 20px 18px 40px; }
              h1, h2, h3 { font-family: Georgia, serif; line-height: 1.2; }
              h1 { font-size: 30px; margin: 0 0 6px; }
              .byline { color: #9a9ca4; font-size: 14px; margin: 0 0 24px; }
              .draft { color: #f2a541; font-size: 14px; }
              a { color: #f2a541; }
              img { max-width: 100%; height: auto; border-radius: 10px; display: block; }
              figure { margin: 24px 0; }
              figcaption { color: #9a9ca4; font-size: 14px; margin-top: 6px; }
              blockquote { border-left: 3px solid #f2a541; margin: 16px 0; padding-left: 14px; color: #d0d0cb; }
            </style></head>
            <body>
              <h1>{{WebUtility.HtmlEncode(post.Title)}}</h1>
              <p class="byline">by {{WebUtility.HtmlEncode(post.Author.DisplayName)}} · {{date}}</p>
              {{draft}}
              {{post.BodyHtml}}
            </body></html>
            """;
    }
}

public partial class PostCommentsViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid _postId;

    [ObservableProperty]
    public partial string Title { get; set; } = "Comments";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommentCommand))]
    public partial string NewComment { get; set; } = "";

    public ObservableCollection<BlogCommentDto> Comments { get; } = [];

    public int CommentMaxLength => BlogLimits.CommentMax;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("title", out var title)) Title = title as string ?? Title;
        if (query.TryGetValue("id", out var id) && id is Guid postId)
        {
            _postId = postId;
            _ = RunAsync(async () =>
            {
                Comments.Clear();
                foreach (var comment in await api.GetBlogCommentsAsync(postId)) Comments.Add(comment);
            });
        }
    }

    private bool CanAddComment() => !string.IsNullOrWhiteSpace(NewComment);

    [RelayCommand(CanExecute = nameof(CanAddComment))]
    private Task AddCommentAsync() => RunAsync(async () =>
    {
        Comments.Add(await api.AddBlogCommentAsync(_postId, NewComment.Trim()));
        NewComment = "";
    });

    [RelayCommand]
    private Task DeleteCommentAsync(BlogCommentDto comment) => RunAsync(async () =>
    {
        if (!await Ui.ConfirmAsync("Delete comment", "Delete this comment?", "Delete")) return;
        await api.DeleteBlogCommentAsync(_postId, comment.Id);
        Comments.Remove(comment);
    });

    [RelayCommand]
    private Task ReportCommentAsync(BlogCommentDto comment) => ReportAsync(r => api.ReportPostAsync(_postId, comment.Id, r));
}
