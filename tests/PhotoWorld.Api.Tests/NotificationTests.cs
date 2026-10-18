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
        Assert.Equal("New on PhotoWorld: Golden hour", email.Subject);
        Assert.Contains("https://photoworld.example/Blogs/Post/11111111-1111-1111-1111-111111111111", email.HtmlBody);
    }

    [Theory]
    [InlineData(NotificationFrequency.Daily, "Your PhotoWorld daily digest: 3 new blog posts")]
    [InlineData(NotificationFrequency.Weekly, "Your PhotoWorld weekly digest: 3 new blog posts")]
    [InlineData(NotificationFrequency.Instant, "3 new blog posts on PhotoWorld")]
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
}
