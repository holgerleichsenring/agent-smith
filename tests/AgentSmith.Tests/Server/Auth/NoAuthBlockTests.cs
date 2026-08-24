using System.Net.Http.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: the configuration every existing installation has. Reporting it would put a
/// permanent advisory on every server that has not adopted authentication yet, which is
/// noise an operator learns to scroll past — and the next real finding scrolls past with it.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class NoAuthBlockTests(NoAuthorityFixture fixture) : IClassFixture<NoAuthorityFixture>
{
    [Fact]
    public async Task Startup_NoAuthBlockAtAll_RecordsNoAuthFinding()
    {
        var response = await fixture.Server.Client
            .GetFromJsonAsync<StartupFindingsResponse>("/api/config/findings");

        response!.Findings.Should().NotContain(f => f.Subsystem == StartupSubsystems.Auth);
    }
}
