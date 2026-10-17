using PhotoWorld.Shared.Auth;

namespace PhotoWorld.Api.Tests;

/// <summary>The app sign-in hand-off: only the holder of the verifier can redeem a code.</summary>
public class AppSignInTests
{
    [Fact]
    public void Challenge_is_the_RFC_7636_S256_value()
    {
        // Test vector from RFC 7636, appendix B.
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            AppSignIn.ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void Matching_verifier_is_accepted()
    {
        var verifier = AppSignIn.NewVerifier();
        Assert.True(AppSignIn.VerifierMatches(verifier, AppSignIn.ChallengeFor(verifier)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public void Missing_or_short_verifier_is_rejected(string? verifier)
    {
        Assert.False(AppSignIn.VerifierMatches(verifier, AppSignIn.ChallengeFor(AppSignIn.NewVerifier())));
    }

    [Fact]
    public void Someone_elses_verifier_is_rejected()
    {
        var challenge = AppSignIn.ChallengeFor(AppSignIn.NewVerifier());
        Assert.False(AppSignIn.VerifierMatches(AppSignIn.NewVerifier(), challenge));
    }

    [Fact]
    public void New_verifiers_and_challenges_are_well_formed_and_unique()
    {
        var verifier = AppSignIn.NewVerifier();
        Assert.Equal(43, verifier.Length);
        Assert.True(AppSignIn.IsWellFormedChallenge(AppSignIn.ChallengeFor(verifier)));
        Assert.NotEqual(verifier, AppSignIn.NewVerifier());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM")] // '+' is not base64url
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM=")] // padded
    public void Malformed_challenges_are_rejected(string? challenge)
    {
        Assert.False(AppSignIn.IsWellFormedChallenge(challenge));
    }
}
