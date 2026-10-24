using PhotoWorld.Shared.Portfolios;

namespace PhotoWorld.Api.Tests;

public class PortfolioTests
{
    [Theory]
    [InlineData("ana-silva", true)]
    [InlineData("ana2026", true)]
    [InlineData("ab", false)]
    [InlineData("Ana-Silva", false)]
    [InlineData("ana--silva", false)]
    [InlineData("-ana", false)]
    [InlineData("ana-", false)]
    [InlineData("ana silva", false)]
    [InlineData("ana_silva", false)]
    public void Addresses_are_short_lower_case_words_with_single_hyphens(string slug, bool valid)
    {
        Assert.Equal(valid, PortfolioRules.IsValidSlug(slug));
    }

    [Theory]
    [InlineData("Ana Silva", "ana-silva")]
    [InlineData("José Löw-Gómez", "jose-low-gomez")]
    [InlineData("  ✨ Studio 21 ✨ ", "studio-21")]
    [InlineData("李", "photographer")]
    public void Suggested_addresses_come_from_the_name(string name, string expected)
    {
        Assert.Equal(expected, PortfolioRules.SuggestSlug(name));
    }

    [Theory]
    [InlineData("@ana.silva", "ana.silva")]
    [InlineData("ana_silva", "ana_silva")]
    [InlineData("https://www.instagram.com/ana.silva/?hl=en", "ana.silva")]
    [InlineData("not a handle!", null)]
    [InlineData("  ", null)]
    public void Instagram_accepts_a_handle_or_a_profile_link(string input, string? expected)
    {
        Assert.Equal(expected, PortfolioRules.InstagramHandle(input));
    }

    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("http://ana.photo/work", "http://ana.photo/work")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("ftp://example.com", null)]
    [InlineData("localhost", null)]
    public void Websites_must_be_web_addresses(string input, string? expected)
    {
        Assert.Equal(expected, PortfolioRules.Website(input));
    }
}
