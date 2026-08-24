using System.Net.Http.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: an auth block that is present but names no authority. The YAML loader ignores
/// unmatched properties, so this is what a typo looks like from the outside — silence.
/// The finding is what turns it back into something an operator can see.
/// <para>
/// The assertions read the AUTH finding's own severity and never the degraded flag: the
/// boot has no Redis by design, so every case here reports degraded no matter what the
/// auth block says.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class UnusableAuthBlockTests(UnusableAuthorityFixture fixture)
    : IClassFixture<UnusableAuthorityFixture>
{
    [Fact]
    public async Task Startup_AuthBlockPresentButUnusable_RecordsAnAdvisoryFinding()
    {
        var finding = await AuthFinding();

        finding.Should().NotBeNull("a misspelled key must not be silence");
        finding!.Severity.Should().Be("advisory",
            "an installation with no authentication is the state every installation was in "
            + "before this phase — it is not a broken one");
    }

    [Fact]
    public async Task Startup_AuthBlockPresentButUnusable_TheFindingCarriesNoProject()
    {
        var finding = await AuthFinding();

        finding!.Project.Should().BeNull();
        finding.Trigger.Should().BeNull();
    }

    [Fact]
    public void Startup_AuthBlockPresentButUnusable_DisablesNoTrigger()
    {
        var findings = fixture.Server.Services.GetRequiredService<IStartupFindings>();

        findings.All.Should().Contain(f => f.Subsystem == StartupSubsystems.Auth,
            "the finding under test has to exist for the rest of this to mean anything");
        findings.IsTriggerBlocked("any-project", "github_trigger").Should().BeFalse();
    }

    private async Task<StartupFindingView?> AuthFinding()
    {
        var response = await fixture.Server.Client
            .GetFromJsonAsync<StartupFindingsResponse>("/api/config/findings");
        return response!.Findings.FirstOrDefault(f => f.Subsystem == StartupSubsystems.Auth);
    }
}
