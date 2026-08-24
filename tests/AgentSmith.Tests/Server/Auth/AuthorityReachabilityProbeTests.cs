using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503e: the probe on its own. What it records is what an operator reads on the findings
/// page, and what it clears is the only reason that page ever stops saying so.
/// </summary>
public sealed class AuthorityReachabilityProbeTests
{
    /// <summary>A loopback port nothing listens on — a refused connection, not a slow one.</summary>
    private const string DeadAuthority = "http://127.0.0.1:1";

    private readonly StartupFindings _findings = new();

    private readonly IHttpClientFactory _clients = new ServiceCollection()
        .AddHttpClient().BuildServiceProvider().GetRequiredService<IHttpClientFactory>();

    [Fact]
    public async Task AuthProbe_AuthorityReachable_RecordsNothing()
    {
        await using var authority = await FlakyAuthority.StartAsync();

        await Probe(authority.Authority).ProbeAsync(CancellationToken.None);

        _findings.All.Should().BeEmpty();
    }

    [Fact]
    public async Task AuthProbe_AuthorityUnreachable_RecordsAFindingCarryingNoProject()
    {
        var probe = Probe(DeadAuthority);

        await probe.ProbeAsync(CancellationToken.None);

        probe.IsUnreachable.Should().BeTrue();
        var finding = _findings.All.Should().ContainSingle().Subject;
        finding.Subsystem.Should().Be(StartupSubsystems.Auth);
        finding.Project.Should().BeNull("an authority nobody can reach is not one project's fault");
        finding.Reason.Should().Contain(DeadAuthority);
    }

    [Fact]
    public async Task AuthProbe_AuthorityRecovers_TheSecondPassClearsIt()
    {
        await using var authority = await FlakyAuthority.StartAsync();
        var probe = Probe(authority.Authority);
        authority.Serving = false;
        await probe.ProbeAsync(CancellationToken.None);
        _findings.All.Should().NotBeEmpty("the first pass must fail for the second to mean anything");

        authority.Serving = true;
        await probe.ProbeAsync(CancellationToken.None);

        _findings.All.Should().BeEmpty();
        probe.IsUnreachable.Should().BeFalse();
    }

    [Fact]
    public async Task AuthProbe_EnforcementOn_TheFindingIsBlocking()
    {
        await Probe(DeadAuthority, enforce: true).ProbeAsync(CancellationToken.None);

        _findings.All.Single().Severity.Should().Be(StartupFindingSeverity.Blocking);
    }

    [Fact]
    public async Task AuthProbe_EnforcementOff_TheFindingIsAdvisory()
    {
        await Probe(DeadAuthority, enforce: false).ProbeAsync(CancellationToken.None);

        var finding = _findings.All.Single();
        finding.Severity.Should().Be(StartupFindingSeverity.Advisory,
            "with the switch off nothing is refused, and a red banner on every installation "
            + "that has not turned enforcement on yet is what p0503b refused");
        finding.Reason.Should().Contain("no route is refused");
    }

    [Fact]
    public async Task AuthProbe_AuthorityUnreachable_DisablesNoTrigger()
    {
        await Probe(DeadAuthority, enforce: true).ProbeAsync(CancellationToken.None);

        _findings.Blocking().Should().NotBeEmpty("the finding has to be blocking for this to bite");
        _findings.IsTriggerBlocked("any-project", "github_trigger").Should().BeFalse();
    }

    private AuthorityReachabilityProbe Probe(string authority, bool enforce = true) =>
        new(new TokenAuthorityConfig { Authority = authority, Enforce = enforce },
            _findings, _clients, NullLogger<AuthorityReachabilityProbe>.Instance);
}
