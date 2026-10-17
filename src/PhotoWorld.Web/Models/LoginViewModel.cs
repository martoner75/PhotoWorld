namespace PhotoWorld.Web.Models;

public sealed record LoginViewModel(
    bool IsRegister,
    string? ReturnUrl,
    string? Error,
    IReadOnlyList<ProviderOption> Providers);

/// <summary>A sign-in button; <see cref="IsConfigured"/> is false when the provider has no credentials yet.</summary>
public sealed record ProviderOption(string Name, bool IsConfigured);
