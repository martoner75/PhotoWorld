using System.Net;
using System.Text;
using PhotoWorld.Shared.Notifications;

namespace PhotoWorld.Api.Services.Notifications;

/// <summary>A published post, as listed in a notification email.</summary>
public sealed record PostNotice(Guid Id, string Title, string Excerpt, string AuthorName, string? CoverUrl);

/// <summary>Builds the notification emails. Everything a member wrote is HTML-encoded.</summary>
public sealed class NotificationEmails(string webBaseUrl)
{
    private readonly string _web = webBaseUrl.TrimEnd('/') + "/";

    public string ConfirmUrl(string token) => $"{_web}Notifications/Confirm?token={Uri.EscapeDataString(token)}";
    public string UnsubscribeUrl(string token) => $"{_web}Notifications/Unsubscribe?token={Uri.EscapeDataString(token)}";
    public string PostUrl(Guid id) => $"{_web}Blogs/Post/{id}";
    private string ManageUrl => $"{_web}Account/Settings#notifications";
    private string BlogsUrl => $"{_web}Blogs";

    public EmailMessage Confirmation(string to, string token)
    {
        var url = ConfirmUrl(token);
        var html = Layout($"""
            <h1 style="font-family:Georgia,serif;font-size:24px;margin:0 0 12px">Confirm your email</h1>
            <p>You asked PhotoWorld to email you about new blog posts at <strong>{E(to)}</strong>.
               Confirm it's you to start receiving them.</p>
            <p style="margin:24px 0">{Button(url, "Confirm my email")}</p>
            <p style="color:#6b6d75;font-size:13px">This link works for {NotificationRules.ConfirmLinkLifetime.TotalDays:0} days.
               If you didn't ask for this, ignore this email and nothing will be sent.</p>
            """, footer: "");
        var text = $"""
            Confirm your email

            You asked PhotoWorld to email you about new blog posts at {to}. Confirm it's you to start receiving them:
            {url}

            This link works for {NotificationRules.ConfirmLinkLifetime.TotalDays:0} days. If you didn't ask for this, ignore this email.
            """;
        return new EmailMessage(to, "Confirm your PhotoWorld notifications", html, text);
    }

    /// <param name="moreCount">Posts beyond <paramref name="posts"/> in this period, summed up with a link.</param>
    public EmailMessage NewPosts(string to, string frequency, IReadOnlyList<PostNotice> posts, int moreCount, string unsubscribeToken)
    {
        var total = posts.Count + moreCount;
        var subject = (frequency, total) switch
        {
            (NotificationFrequency.Daily, _) => $"Your PhotoWorld daily digest: {Count(total)}",
            (NotificationFrequency.Weekly, _) => $"Your PhotoWorld weekly digest: {Count(total)}",
            (_, 1) => $"New on PhotoWorld: {posts[0].Title}",
            _ => $"{Count(total)} on PhotoWorld",
        };
        var unsubscribe = UnsubscribeUrl(unsubscribeToken);

        var html = new StringBuilder($"""<h1 style="font-family:Georgia,serif;font-size:24px;margin:0 0 20px">{E(Count(total))} on PhotoWorld</h1>""");
        var text = new StringBuilder($"{Count(total)} on PhotoWorld\n\n");
        foreach (var post in posts)
        {
            var url = PostUrl(post.Id);
            html.Append($"""
                <div style="margin:0 0 24px;padding:0 0 20px;border-bottom:1px solid #e4e4e0">
                  {(post.CoverUrl is null ? "" : $"""<a href="{E(url)}"><img src="{E(post.CoverUrl)}" alt="" width="520" style="width:100%;max-width:520px;border-radius:10px;display:block;margin:0 0 12px"></a>""")}
                  <h2 style="font-family:Georgia,serif;font-size:20px;margin:0 0 4px"><a href="{E(url)}" style="color:#1a1a1a;text-decoration:none">{E(post.Title)}</a></h2>
                  <p style="color:#6b6d75;font-size:13px;margin:0 0 8px">by {E(post.AuthorName)}</p>
                  <p style="margin:0 0 10px">{E(post.Excerpt)}</p>
                  <a href="{E(url)}" style="color:#b86e0a;font-weight:600">Read the post</a>
                </div>
                """);
            text.Append($"{post.Title}\nby {post.AuthorName}\n{post.Excerpt}\n{url}\n\n");
        }
        if (moreCount > 0)
        {
            html.Append($"""<p>And {moreCount} more: <a href="{E(BlogsUrl)}" style="color:#b86e0a">see all new posts</a>.</p>""");
            text.Append($"And {moreCount} more: {BlogsUrl}\n\n");
        }

        var footer = $"""
            You're getting this because you subscribed to new blog posts on PhotoWorld with {E(to)}.
            <a href="{E(ManageUrl)}" style="color:#6b6d75">Change how often</a> ·
            <a href="{E(unsubscribe)}" style="color:#6b6d75">Unsubscribe</a>
            """;
        text.Append($"You're getting this because you subscribed to new blog posts on PhotoWorld with {to}.\n" +
                    $"Change how often: {ManageUrl}\nUnsubscribe: {unsubscribe}\n");

        // RFC 8058 one-click unsubscribe: email apps show an Unsubscribe button that POSTs to this address.
        var headers = new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = $"<{unsubscribe}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return new EmailMessage(to, subject, Layout(html.ToString(), footer), text.ToString(), headers);
    }

    private static string Count(int n) => n == 1 ? "1 new blog post" : $"{n} new blog posts";

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Button(string url, string label) =>
        $"""<a href="{E(url)}" style="background:#f2a541;color:#1a1206;font-weight:600;padding:12px 20px;border-radius:10px;text-decoration:none;display:inline-block">{E(label)}</a>""";

    private string Layout(string content, string footer) => $"""
        <!DOCTYPE html>
        <html><body style="margin:0;background:#f4f4f1;font-family:-apple-system,'Segoe UI',Helvetica,Arial,sans-serif;font-size:15px;line-height:1.55;color:#1a1a1a">
          <div style="max-width:560px;margin:0 auto;padding:28px 20px">
            <p style="margin:0 0 20px;font-family:Georgia,serif;font-weight:700;font-size:18px">
              <a href="{E(_web)}" style="color:#1a1a1a;text-decoration:none"><span style="color:#f2a541">&#9673;</span> PhotoWorld</a>
            </p>
            <div style="background:#ffffff;border-radius:14px;padding:28px 24px">{content}</div>
            <p style="color:#6b6d75;font-size:12px;margin:18px 4px 0">{footer}</p>
          </div>
        </body></html>
        """;
}
