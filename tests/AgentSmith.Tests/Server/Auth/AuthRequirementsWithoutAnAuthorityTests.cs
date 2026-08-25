using System.Net.Http.Json;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-08-25-4530: the configuration every installation had before p0503b — no auth block
/// at all. Nothing about it is a defect, and the answer has to say so plainly, or a
/// dashboard reading it raises a banner about a server that demands nothing.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class AuthRequirementsWithoutAnAuthorityTests(NoAuthorityFixture fixture)
    : IClassFixture<NoAuthorityFixture>
{
    [Fact]
    public async Task Requirements_NoAuthority_SaysEnforcementIsOff()
    {
        var requirements = await fixture.Server.Client
            .GetFromJsonAsync<AuthRequirements>("/api/auth/requirements");

        requirements!.Enforced.Should().BeFalse();
        requirements.Authority.Should().BeNull("nothing is configured, and an empty string "
            + "is a value a dashboard would try to compare against");
        requirements.Audience.Should().BeNull();
    }
}
