using PhotoWorld.Api.Services.Notifications;
using PhotoWorld.Shared.Notifications;

namespace PhotoWorld.Api.Tests;

public class NotificationRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Instant_subscribers_are_always_due()
    {
        Assert.True(NotificationRules.IsDue(NotificationFrequency.Instant, Start, Start));
    }

    [Theory]
    [InlineData(NotificationFrequency.Daily, 23, false)]
    [InlineData(NotificationFrequency.Daily, 24, true)]
    [InlineData(NotificationFrequency.Weekly, 24 * 7 - 1, false)]
    [InlineData(NotificationFrequency.Weekly, 24 * 7, true)]
    public void Digests_are_due_once_their_period_has_passed(string frequency, int hoursLater, bool due)
    {
        Assert.Equal(due, NotificationRules.IsDue(frequency, Start, Start.AddHours(hoursLater)));
    }

    [Fact]
    public void Tokens_are_random_url_safe_and_stored_only_as_a_hash()
    {
        var token = NotificationRules.NewToken();
        Assert.Equal(43, token.Length);
        Assert.All(token, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.NotEqual(token, NotificationRules.NewToken());

        var hash = NotificationRules.Hash(token);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, NotificationRules.Hash(token));
        Assert.NotEqual(hash, NotificationRules.Hash(NotificationRules.NewToken()));
    }

    [Theory]
    [InlineData("Ana@Example.com", "ana@example.com", true)]
    [InlineData(" ana@example.com ", "ana@example.com", true)]
    [InlineData("other@example.com", "ana@example.com", false)]
    [InlineData("ana@example.com", null, false)]
    public void Only_the_verified_account_email_skips_confirmation(string email, string? accountEmail, bool trusted)
    {
        Assert.Equal(trusted, NotificationRules.IsAccountEmail(email, accountEmail));
    }

    [Theory]
    [InlineData("instant", true)]
    [InlineData("weekly", true)]
    [InlineData("hourly", false)]
    [InlineData(null, false)]
    public void Only_known_frequencies_are_accepted(string? frequency, bool valid)
    {
        Assert.Equal(valid, NotificationFrequency.IsValid(frequency));
    }
}

public class NotificationEmailsTests
{
    private readonly NotificationEmails _emails = new("https://photoworld.example");

    private static PostNotice Post(string title = "Golden hour", string author = "Ana") =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), title, "A walk by the river.", author, null);

    [Fact]
    public void One_instant_post_is_named_in_the_subject()
    {
        var email = _emails.NewPosts("me@example.com", NotificationFrequency.Instant, [Post()], 0, "unsub-token");
        Assert.Equal("New on PhotoCommunity: Golden hour", email.Subject);
        Assert.Contains("https://photoworld.example/Blogs/Post/11111111-1111-1111-1111-111111111111", email.HtmlBody);
    }

    [Theory]
    [InlineData(NotificationFrequency.Daily, "Your PhotoCommunity daily digest: 3 new blog posts")]
    [InlineData(NotificationFrequency.Weekly, "Your PhotoCommunity weekly digest: 3 new blog posts")]
    [InlineData(NotificationFrequency.Instant, "3 new blog posts on PhotoCommunity")]
    public void Digests_and_batches_count_every_post_including_the_ones_not_listed(string frequency, string subject)
    {
        var email = _emails.NewPosts("me@example.com", frequency, [Post(), Post("Fog")], 1, "unsub-token");
        Assert.Equal(subject, email.Subject);
        Assert.Contains("And 1 more", email.HtmlBody);
    }

    [Fact]
    public void Member_text_is_html_encoded()
    {
        var email = _emails.NewPosts("me@example.com", NotificationFrequency.Instant,
            [Post("<script>alert(1)</script>", "<b>Eve</b>")], 0, "unsub-token");
        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.DoesNotContain("<b>Eve</b>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
    }

    [Fact]
    public void Every_post_email_has_one_click_unsubscribe()
    {
        var email = _emails.NewPosts("me@example.com", NotificationFrequency.Daily, [Post()], 0, "abc-123");
        const string url = "https://photoworld.example/Notifications/Unsubscribe?token=abc-123";
        Assert.Equal($"<{url}>", email.Headers!["List-Unsubscribe"]);
        Assert.Equal("List-Unsubscribe=One-Click", email.Headers["List-Unsubscribe-Post"]);
        Assert.Contains(url, email.HtmlBody);
        Assert.Contains(url, email.TextBody);
    }

    [Fact]
    public void Confirmation_links_to_the_website_with_the_token()
    {
        var email = _emails.Confirmation("me@example.com", "tok_en");
        Assert.Contains("https://photoworld.example/Notifications/Confirm?token=tok_en", email.HtmlBody);
        Assert.Contains("https://photoworld.example/Notifications/Confirm?token=tok_en", email.TextBody);
        Assert.Null(email.Headers);
    }

    [Fact]
    public void Welcome_greets_the_member_and_links_to_getting_started()
    {
        var email = _emails.Welcome("me@example.com", "Ana <b>");
        Assert.Equal("Welcome to PhotoCommunity", email.Subject);
        Assert.Contains("Ana &lt;b&gt;", email.HtmlBody);
        Assert.Contains("https://photoworld.example/Photos/Upload", email.HtmlBody);
        Assert.Contains("https://photoworld.example/Account/Settings#notifications", email.TextBody);
        Assert.Null(email.Headers);
    }

    [Fact]
    public void Report_alert_names_the_item_and_reason_and_encodes_the_note()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var email = _emails.ReportAlert("admin@example.com", "Photo", "Spam or advertising", "<script>x</script>", 1, false, id);

        Assert.Equal("PhotoCommunity: Photo reported (Spam or advertising)", email.Subject);
        Assert.Contains("&lt;script&gt;x&lt;/script&gt;", email.HtmlBody);
        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.Contains(id.ToString(), email.TextBody);
        Assert.Contains("stays visible", email.TextBody);
        Assert.Null(email.Headers);
    }

    [Fact]
    public void Report_alert_says_when_the_item_was_auto_hidden()
    {
        var email = _emails.ReportAlert("admin@example.com", "Blog post", "Harassment or hate", null, 3, true, Guid.NewGuid());

        Assert.Equal("PhotoCommunity: Blog post hidden after 3 reports (Harassment or hate)", email.Subject);
        Assert.Contains("hidden from everyone except its owner", email.TextBody);
        Assert.DoesNotContain("Reporter's note", email.TextBody);
    }
}

public class WeeklySummaryEmailTests
{
    private readonly NotificationEmails _emails = new("https://photoworld.example");

    [Fact]
    public void Lists_only_what_happened_and_leads_with_it_in_the_subject()
    {
        var email = _emails.WeeklySummary("me@example.com", "Ana", new WeeklyStats(1240, 1, 0, 0, 3, 0, 0), "unsub-token");

        Assert.Equal("Your week on PhotoCommunity: 1,240 views of your photos", email.Subject);
        Assert.Contains("1 like", email.TextBody);
        Assert.Contains("3 new followers", email.TextBody);
        Assert.DoesNotContain("comment", email.TextBody);
        Assert.DoesNotContain("walk", email.TextBody);
        Assert.Contains("https://photoworld.example/Members/Insights", email.HtmlBody);
    }

    [Fact]
    public void Mentions_walks_and_challenges_and_unsubscribes_from_everything()
    {
        var email = _emails.WeeklySummary("me@example.com", "Ana <b>", new WeeklyStats(0, 0, 0, 0, 0, 2, 1), "unsub-token");

        Assert.Equal("Your week on PhotoCommunity", email.Subject);
        Assert.Contains("2 photo walks by people you follow", email.TextBody);
        Assert.Contains("1 photo challenge is open", email.TextBody);
        Assert.Contains("Ana &lt;b&gt;", email.HtmlBody);
        Assert.Contains("Unsubscribe from all PhotoCommunity emails", email.HtmlBody);
        Assert.Equal("<https://photoworld.example/Notifications/Unsubscribe?token=unsub-token>", email.Headers!["List-Unsubscribe"]);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 5, true)]   // open challenges alone aren't news
    [InlineData(1, 0, 0, 0, 0, 0, 0, false)]
    [InlineData(0, 0, 0, 0, 0, 1, 0, false)]
    public void A_quiet_week_sends_nothing(int views, int likes, int comments, int feedback, int followers, int walks, int challenges, bool quiet)
    {
        Assert.Equal(quiet, new WeeklyStats(views, likes, comments, feedback, followers, walks, challenges).IsQuiet);
    }
}
