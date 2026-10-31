namespace PhotoWorld.Shared.Portfolios;

/// <summary>One public portfolio, as listed for the website's sitemap.</summary>
public sealed record PortfolioIndexEntryDto(string Slug, DateTimeOffset? UpdatedAt);
