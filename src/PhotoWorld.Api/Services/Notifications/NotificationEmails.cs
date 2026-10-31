using System.Net;
using System.Text;
using PhotoWorld.Shared.Notifications;

namespace PhotoWorld.Api.Services.Notifications;

/// <summary>A published post, as listed in a notification email.</summary>
/// <summary>A member's last 7 days, for the weekly summary.</summary>
public sealed record WeeklyStats(int Views, int Likes, int Comments, int Feedback, int NewFollowers, int UpcomingWalks, int OpenChallenges)
{
    /// <summary>Nothing on their work and no walks coming up: no email (open challenges alone aren't news).</summary>
    public bool IsQuiet => Views + Likes + Comments + Feedback + NewFollowers + UpcomingWalks == 0;
}

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
            <p>You asked PhotoCommunity to email you about new blog posts at <strong>{E(to)}</strong>.
               Confirm it's you to start receiving them.</p>
            <p style="margin:24px 0">{Button(url, "Confirm my email")}</p>
            <p style="color:#6b6d75;font-size:13px">This link works for {NotificationRules.ConfirmLinkLifetime.TotalDays:0} days.
               If you didn't ask for this, ignore this email and nothing will be sent.</p>
            """, footer: "");
        var text = $"""
            Confirm your email

            You asked PhotoCommunity to email you about new blog posts at {to}. Confirm it's you to start receiving them:
            {url}

            This link works for {NotificationRules.ConfirmLinkLifetime.TotalDays:0} days. If you didn't ask for this, ignore this email.
            """;
        return new EmailMessage(to, "Confirm your PhotoCommunity notifications", html, text);
    }

    /// <summary>Sent once, when someone first joins. Not a subscription, so no unsubscribe link.</summary>
    public EmailMessage Welcome(string to, string name)
    {
        var html = Layout($"""
            <h1 style="font-family:Georgia,serif;font-size:24px;margin:0 0 12px">Welcome to PhotoCommunity</h1>
            <p style="margin:0 0 14px">Hi {E(name)},</p>
            <p style="margin:0 0 14px">Thanks for joining. A few ways to get started:</p>
            <ul style="margin:0 0 18px;padding-left:20px">
              <li><a href="{E(UploadUrl)}" style="color:#b86e0a">Share your first photo</a></li>
              <li><a href="{E(ClubsUrl)}" style="color:#b86e0a">Join a club</a> and meet photographers near you</li>
              <li><a href="{E(WalksUrl)}" style="color:#b86e0a">Find a photo walk</a> or <a href="{E(ChallengesUrl)}" style="color:#b86e0a">enter a challenge</a></li>
              <li><a href="{E(ManageUrl)}" style="color:#b86e0a">Choose your emails</a>: new blog posts and a weekly summary</li>
            </ul>
            <p style="margin:24px 0 0">{Button(UploadUrl, "Share a photo")}</p>
            """, footer: $"You're getting this because you just joined PhotoCommunity with {E(to)}. It's a one-off email.");
        var text = $"""
            Welcome to PhotoCommunity

            Hi {name},

            Thanks for joining. A few ways to get started:
            - Share your first photo: {UploadUrl}
            - Join a club and meet photographers near you: {ClubsUrl}
            - Find a photo walk: {WalksUrl}
            - Enter a challenge: {ChallengesUrl}
            - Choose your emails (new blog posts and a weekly summary): {ManageUrl}

            You're getting this because you just joined PhotoCommunity with {to}. It's a one-off email.
            """;
        return new EmailMessage(to, "Welcome to PhotoCommunity", html, text);
    }

    /// <summary>
    /// Tells a site admin that something was reported. Sent for the first open report on an item and again when it
    /// reaches the auto-hide threshold. A service message to admins, so no unsubscribe link. Everything is encoded.
    /// </summary>
    public EmailMessage ReportAlert(string to, string kind, string reasonLabel, string? note, int openReports, bool autoHidden, Guid reportId)
    {
        var headline = autoHidden ? $"{kind} hidden after {openReports} reports" : $"{kind} reported";
        var subject = $"PhotoCommunity: {headline} ({reasonLabel})";
        var hiddenLine = autoHidden
            ? "It has been hidden from everyone except its owner until a moderator reviews it."
            : "It stays visible until it has enough open reports or a moderator acts.";
        var noteText = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var html = Layout($"""
            <h1 style="font-family:Georgia,serif;font-size:24px;margin:0 0 12px">{E(headline)}</h1>
            <p style="margin:0 0 14px"><strong>Reason:</strong> {E(reasonLabel)}<br>
               <strong>Open reports on this item:</strong> {openReports}</p>
            {(noteText is null ? "" : $"""<p style="margin:0 0 14px;padding:10px 14px;background:#f4f4f1;border-radius:8px"><strong>Reporter's note:</strong><br>{E(noteText)}</p>""")}
            <p style="margin:0 0 14px">{E(hiddenLine)}</p>
            <p style="margin:0;color:#6b6d75;font-size:13px">Review it in the database: <code>photoworld.reports</code>, report <code>{reportId}</code>.
               Set its status to <code>dismissed</code> or <code>upheld</code>.</p>
            """, footer: "You're getting this because you're a PhotoCommunity admin.");
        var text = $"""
            {headline}

            Reason: {reasonLabel}
            Open reports on this item: {openReports}
            {(noteText is null ? "" : $"Reporter's note: {noteText}\n")}
            {hiddenLine}

            Review it in the database: photoworld.reports, report {reportId}. Set its status to dismissed or upheld.

            You're getting this because you're a PhotoCommunity admin.
            """;
        return new EmailMessage(to, subject, html, text);
    }

    /// <param name="moreCount">Posts beyond <paramref name="posts"/> in this period, summed up with a link.</param>
    public EmailMessage NewPosts(string to, string frequency, IReadOnlyList<PostNotice> posts, int moreCount, string unsubscribeToken)
    {
        var total = posts.Count + moreCount;
        var subject = (frequency, total) switch
        {
            (NotificationFrequency.Daily, _) => $"Your PhotoCommunity daily digest: {Count(total)}",
            (NotificationFrequency.Weekly, _) => $"Your PhotoCommunity weekly digest: {Count(total)}",
            (_, 1) => $"New on PhotoCommunity: {posts[0].Title}",
            _ => $"{Count(total)} on PhotoCommunity",
        };
        var unsubscribe = UnsubscribeUrl(unsubscribeToken);

        var html = new StringBuilder($"""<h1 style="font-family:Georgia,serif;font-size:24px;margin:0 0 20px">{E(Count(total))} on PhotoCommunity</h1>""");
        var text = new StringBuilder($"{Count(total)} on PhotoCommunity\n\n");
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
            You're getting this because you subscribed to new blog posts on PhotoCommunity with {E(to)}.
            <a href="{E(ManageUrl)}" style="color:#6b6d75">Change how often</a> ·
            <a href="{E(unsubscribe)}" style="color:#6b6d75">Unsubscribe</a>
            """;
        text.Append($"You're getting this because you subscribed to new blog posts on PhotoCommunity with {to}.\n" +
                    $"Change how often: {ManageUrl}\nUnsubscribe: {unsubscribe}\n");

        // RFC 8058 one-click unsubscribe: email apps show an Unsubscribe button that POSTs to this address.
        var headers = new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = $"<{unsubscribe}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return new EmailMessage(to, subject, Layout(html.ToString(), footer), text.ToString(), headers);
    }

    /// <summary>The member's week: what happened on their work, walks coming up from people they follow, open challenges.</summary>
    public EmailMessage WeeklySummary(string to, string name, WeeklyStats stats, string unsubscribeToken)
    {
        var unsubscribe = UnsubscribeUrl(unsubscribeToken);
        (int N, string One, string Many)[] rows =
        [
            (stats.Views, "view of your photos", "views of your photos"),
            (stats.Likes, "like", "likes"),
            (stats.Comments, "comment", "comments"),
            (stats.Feedback, "piece of feedback", "pieces of feedback"),
            (stats.NewFollowers, "new follower", "new followers"),
        ];
        var lines = rows.Where(r => r.N > 0).Select(r => $"{r.N:N0} {(r.N == 1 ? r.One : r.Many)}").ToList();
        var subject = lines.Count > 0 ? $"Your week on PhotoCommunity: {lines[0]}" : "Your week on PhotoCommunity";

        var html = new StringBuilder($"""<h1 style="font-family:Georgia,serif;font-size:24px;margin:0 0 16px">Your week on PhotoCommunity</h1>""");
        var text = new StringBuilder($"Your week on PhotoCommunity\n\nHi {name},\n\n");
        html.Append($"""<p style="margin:0 0 14px">Hi {E(name)},</p>""");
        if (lines.Count > 0)
        {
            html.Append("""<p style="margin:0 0 6px">In the last 7 days your photos got:</p><ul style="margin:0 0 18px;padding-left:20px">""");
            foreach (var line in lines) html.Append($"<li>{E(line)}</li>");
            html.Append("</ul>");
            text.Append("In the last 7 days your photos got:\n").Append(string.Concat(lines.Select(l => $"- {l}\n"))).Append('\n');
        }
        if (stats.UpcomingWalks > 0)
        {
            var walks = stats.UpcomingWalks == 1 ? "1 photo walk" : $"{stats.UpcomingWalks} photo walks";
            html.Append($"""<p style="margin:0 0 14px">{walks} by people you follow in the next 7 days. <a href="{E(WalksUrl)}" style="color:#b86e0a">See the walks</a></p>""");
            text.Append($"{walks} by people you follow in the next 7 days: {WalksUrl}\n\n");
        }
        if (stats.OpenChallenges > 0)
        {
            var challenges = stats.OpenChallenges == 1 ? "1 photo challenge is" : $"{stats.OpenChallenges} photo challenges are";
            html.Append($"""<p style="margin:0 0 18px">{challenges} open for entries. <a href="{E(ChallengesUrl)}" style="color:#b86e0a">Take part</a></p>""");
            text.Append($"{challenges} open for entries: {ChallengesUrl}\n\n");
        }
        html.Append(Button(InsightsUrl, "See your insights"));
        text.Append($"See your insights: {InsightsUrl}\n\n");

        var footer = $"""
            You're getting this because you turned on the weekly summary on PhotoCommunity for {E(to)}.
            <a href="{E(ManageUrl)}" style="color:#6b6d75">Email settings</a> ·
            <a href="{E(unsubscribe)}" style="color:#6b6d75">Unsubscribe from all PhotoCommunity emails</a>
            """;
        text.Append($"You're getting this because you turned on the weekly summary on PhotoCommunity for {to}.\n" +
                    $"Email settings: {ManageUrl}\nUnsubscribe from all PhotoCommunity emails: {unsubscribe}\n");
        var headers = new Dictionary<string, string>
        {
            ["List-Unsubscribe"] = $"<{unsubscribe}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return new EmailMessage(to, subject, Layout(html.ToString(), footer), text.ToString(), headers);
    }

    private string WalksUrl => $"{_web}Walks";
    private string ChallengesUrl => $"{_web}Challenges";
    private string InsightsUrl => $"{_web}Members/Insights";
    private string UploadUrl => $"{_web}Photos/Upload";
    private string ClubsUrl => $"{_web}Clubs";

    private static string Count(int n) => n == 1 ? "1 new blog post" : $"{n} new blog posts";

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Button(string url, string label) =>
        $"""<a href="{E(url)}" style="background:#f2a541;color:#1a1206;font-weight:600;padding:12px 20px;border-radius:10px;text-decoration:none;display:inline-block">{E(label)}</a>""";

    private string Layout(string content, string footer) => $"""
        <!DOCTYPE html>
        <html><body style="margin:0;background:#f4f4f1;font-family:-apple-system,'Segoe UI',Helvetica,Arial,sans-serif;font-size:15px;line-height:1.55;color:#1a1a1a">
          <div style="max-width:560px;margin:0 auto;padding:28px 20px">
            <p style="margin:0 0 20px;font-family:Georgia,serif;font-weight:700;font-size:18px">
              <a href="{E(_web)}" style="color:#1a1a1a;text-decoration:none"><span style="color:#f2a541">&#9673;</span> PhotoCommunity</a>
            </p>
            <div style="background:#ffffff;border-radius:14px;padding:28px 24px">{content}</div>
            <p style="color:#6b6d75;font-size:12px;margin:18px 4px 0">{footer}</p>
          </div>
        </body></html>
        """;
}
