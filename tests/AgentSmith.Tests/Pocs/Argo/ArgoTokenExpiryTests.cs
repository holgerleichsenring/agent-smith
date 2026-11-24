using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: the role token's expiry, read from the JWT payload without verifying it.</summary>
public sealed class ArgoTokenExpiryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Read_ExpPresent_IsTheDate()
    {
        var (expiry, refusal) = ArgoTokenExpiry.Read(FakeArgoHost.Jwt(Now.AddDays(60).ToUnixTimeSeconds()));

        refusal.Should().BeNull();
        expiry!.ExpiresAt.Should().Be(Now.AddDays(60));
        expiry.Sentence(Now, 14).Should().Be("The token expires on 2027-01-23.");
        expiry.Warns(Now, 14).Should().BeFalse();
    }

    [Fact]
    public void Read_NoExp_IsNoExpiry()
    {
        var (expiry, _) = ArgoTokenExpiry.Read(FakeArgoHost.Jwt(null));

        expiry!.ExpiresAt.Should().BeNull();
        expiry.Sentence(Now, 14).Should().Be("The token has no expiry.");
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public void Read_NotAJwt_IsRefusedNamingTheKey(string token)
    {
        var (expiry, refusal) = ArgoTokenExpiry.Read(token);

        expiry.Should().BeNull();
        refusal.Should().Contain("pocs.targets[].argo.token_secret");
    }

    [Fact]
    public void Sentence_UnderTokenWarnDays_Warns()
    {
        var (expiry, _) = ArgoTokenExpiry.Read(FakeArgoHost.Jwt(Now.AddDays(3).ToUnixTimeSeconds()));

        expiry!.Warns(Now, 14).Should().BeTrue();
        expiry.Sentence(Now, 14).Should().Contain("in 3 day(s)").And.Contain("token_warn_days (14)");
        expiry.Sentence(Now.AddDays(4), 14).Should().Contain("expired");
    }
}
