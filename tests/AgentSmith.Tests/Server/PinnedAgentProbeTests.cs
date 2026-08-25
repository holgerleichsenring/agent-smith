using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-25-0d01: the other half of the ruling. Deriving the version removed the
/// ACCIDENTAL mismatch; a pin is now the only way to get one, so a pin is judged and seen.
/// Like the build mismatch 2026-08-25-8c97 reports, it says the two are DIFFERENT and never
/// says they are incompatible — a tag is not evidence about a protocol.
/// </summary>
public sealed class PinnedAgentProbeTests
{
    [Fact]
    public async Task AgentVersion_DeliberatelyPinned_IsJudgedAndReported()
    {
        var findings = await Probe(pinned: "0.121.0", serverVersion: "0.135.0").ProbeAsync(default);

        var finding = findings.Should().ContainSingle().Subject;
        finding.Subsystem.Should().Be(StartupSubsystems.SandboxAgent);
        finding.Project.Should().Be("alpha");
        finding.Field.Should().Be("sandbox.agent_version");
        finding.Reason.Should().Contain("0.121.0").And.Contain("0.135.0");
    }

    [Fact]
    public async Task AgentVersion_DeliberatelyPinned_IsAdvisoryAndRefusesNothing()
    {
        var findings = await Probe(pinned: "0.121.0", serverVersion: "0.135.0").ProbeAsync(default);

        findings.Single().Severity.Should().Be(StartupFindingSeverity.Advisory);
        findings.Single().IsBlocking.Should().BeFalse();
    }

    [Fact]
    public async Task AgentVersion_DeliberatelyPinned_IsNeverCalledIncompatible()
    {
        var findings = await Probe(pinned: "0.121.0", serverVersion: "0.135.0").ProbeAsync(default);

        findings.Single().Reason.Should().NotContain("incompat",
            "whether they can talk is a property of the wire between them, which is read "
            + "from what the agent actually answers");
    }

    [Fact]
    public async Task AgentVersion_Derived_ProducesNoFinding()
    {
        var findings = await Probe(pinned: "", serverVersion: "0.135.0").ProbeAsync(default);

        findings.Should().BeEmpty("nothing was declared, so nothing can disagree");
    }

    [Fact]
    public async Task AgentVersion_NeitherDeclaredNorDerivable_IsLeftToTheConfigurationProbe()
    {
        var findings = await Probe(pinned: "", serverVersion: null).ProbeAsync(default);

        findings.Should().BeEmpty("that project's image cannot be resolved at all, which the "
            + "configuration probe already reports — this one has nothing to add");
    }

    private static PinnedAgentProbe Probe(string pinned, string? serverVersion)
    {
        var global = Options.Create(new SandboxGlobalConfig { AgentVersion = pinned });
        var config = new AgentSmithConfig
        {
            Projects = new() { ["alpha"] = new ResolvedProject { Name = "alpha" } }
        };
        return new PinnedAgentProbe(config,
            new AgentVersionResolver(global, new BuildIdentity("deadbeefcafe", serverVersion)));
    }
}
