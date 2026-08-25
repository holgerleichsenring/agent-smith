using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-4530: the two configurations no booted fixture states, because both are
/// about the gap between the switch an operator sets and what the server then does with
/// it. Reported wrong, each one sends the dashboard to raise a banner over an installation
/// that is refusing nobody.
/// </summary>
public sealed class AuthRequirementsTests
{
    [Fact]
    public void From_EnforceOnWithNoAuthority_StillSaysEnforcementIsOff() =>
        AuthRequirements.From(new TokenAuthorityConfig { Enforce = true })
            .Enforced.Should().BeFalse(
                "the fallback policy that refuses anything is attached only once the "
                + "authority is usable, so this installation refuses nothing");

    [Fact]
    public void From_AnAuthorityWithEnforcementOff_NamesItAndSaysEnforcementIsOff()
    {
        var requirements = AuthRequirements.From(new TokenAuthorityConfig
        {
            Authority = "https://login.example.com/realms/example",
            Audience = "agent-smith",
            Enforce = false,
        });

        requirements.Enforced.Should().BeFalse("tokens are validated; nothing is refused");
        requirements.Authority.Should().Be("https://login.example.com/realms/example");
        requirements.Audience.Should().Be("agent-smith");
    }

    [Fact]
    public void From_AnAudienceLeftBlank_ReportsItAbsentRatherThanEmpty() =>
        AuthRequirements.From(new TokenAuthorityConfig
        {
            Authority = "https://login.example.com/realms/example",
            Audience = "   ",
        }).Audience.Should().BeNull("a blank audience is not checked, so it is not demanded");
}
